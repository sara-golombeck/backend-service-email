using EmailServiceAPI.Models;
using EmailServiceAPI.Services;
using EmailServiceAPI.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // React default ports
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add validation
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

// Add database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add custom services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IQueueService, SqsQueueService>();

// Add AWS services
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSQS>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// Minimal API endpoints
app.MapPost("/api/auth/login", async (
    [FromBody] LoginRequest request,
    IValidator<LoginRequest> validator,
    IQueueService queueService,
    AppDbContext context,
    ILogger<Program> logger) =>
{
    try
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Login attempt with invalid email: {Email}", request.Email);
            return Results.BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid email format",
                Errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList()
            });
        }

        // Save user login attempt to database
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            user = new User 
            { 
                Email = request.Email,
                LoginAttempts = 1,
                LastLoginAttempt = DateTime.UtcNow
            };
            context.Users.Add(user);
        }
        else
        {
            user.LoginAttempts++;
            user.LastLoginAttempt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        
        logger.LogInformation("User login attempt recorded: {Email}, Total attempts: {Attempts}", 
            request.Email, user.LoginAttempts);

        // Send email request to queue
        await queueService.SendToEmailQueueAsync(request.Email);
        
        logger.LogInformation("Email request queued successfully for: {Email}", request.Email);
        return Results.Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Email queued for sending"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during login process for email: {Email}", request.Email);
        return Results.Problem(
            detail: "An unexpected error occurred",
            statusCode: 500,
            title: "Internal server error"
        );
    }
})
.WithName("Login")
.Produces<ApiResponse<object>>(200)
.Produces<ApiResponse<object>>(400)
.Produces(500);

// Health check endpoint
app.MapGet("/api/health", () =>
{
    return Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
})
.WithName("HealthCheck");
app.Run();