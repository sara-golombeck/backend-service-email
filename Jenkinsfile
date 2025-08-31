pipeline {
    agent any
    
    environment {
        
        APP_NAME = 'automarkly-backend'
        BUILD_NUMBER = "${env.BUILD_NUMBER}"
        AWS_ACCOUNT_ID = credentials('aws-account-id')
        AWS_REGION = credentials('aws_region')
        ECR_REPO_BACKEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-backend"
        GITOPS_REPO = 'git@github.com:sara-golombeck/gitops-email-service.git'
        HELM_VALUES_PATH = 'charts/email-service/values.yaml'
        
        // Images for E2E tests
FRONTEND_IMAGE = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-frontend"
WORKER_IMAGE = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-worker"    }
    
    triggers {
        githubPush()
    }
    stages {
        stage('Checkout') {
            steps {
                checkout scm
                sshagent(['github']) {
                    sh "git fetch --tags --unshallow || git fetch --tags"
                }
            }
        }
        
        stage('Unit Tests') {
            steps {
                sh '''
                    docker build -f Dockerfile.test -t "${APP_NAME}:test-${BUILD_NUMBER}" .
                    mkdir -p test-results
                    docker run --rm \
                        -v "${PWD}/test-results:/app/test-results" \
                        "${APP_NAME}:test-${BUILD_NUMBER}"
                '''
            }
            post {
                always {
                    archiveArtifacts artifacts: 'test-results/**/*', allowEmptyArchive: true
                }
            }
        }
        
        stage('Build Applications') {
            steps {
                script {
                    docker.build("${APP_NAME}:${BUILD_NUMBER}")
                }
            }
        }
        
        stage('E2E Tests') {
            steps {
                sh '''
                    cat > .env <<EOF
POSTGRES_DB=automarkly_test
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres123
COMPOSE_PROJECT_NAME=automarkly_e2e
FRONTEND_IMAGE=${FRONTEND_IMAGE}
WORKER_IMAGE=${WORKER_IMAGE}
BUILD_NUMBER=${BUILD_NUMBER}
EOF
                    
                    docker compose -f docker-compose.e2e.yml up -d
                                
            echo "=== מצב קונטיינרים אחרי 5 שניות ==="
            sleep 5
            docker ps
            
            echo "=== לוגים של כל הקונטיינרים ==="
            docker logs emailservice-frontend || echo "אין לוגים מ-frontend"
            docker logs emailservice-backend || echo "אין לוגים מ-backend"  
            docker logs emailservice-worker || echo "אין לוגים מ-worker"
            docker logs automarkly_e2e_postgres || echo "אין לוגים מ-postgres"
            
            echo "=== מצב קונטיינרים אחרי 15 שניות נוספות ==="
            sleep 15
            docker ps

                    docker build -f tests/integration/Dockerfile -t "${APP_NAME}:e2e-${BUILD_NUMBER}" tests/integration/
                    sleep 15
                    docker run --rm \
                        --network automarkly_e2e_app-network \
                        -e BASE_URL=http://emailservice-frontend:80 \
                        "${APP_NAME}:e2e-${BUILD_NUMBER}"
                '''
            }
            post {
                always {
                    sh '''
                        docker compose -f docker-compose.e2e.yml logs > e2e-logs.txt || true
                        docker compose -f docker-compose.e2e.yml down -v || true
                    '''
                    archiveArtifacts artifacts: 'e2e-logs.txt', allowEmptyArchive: true
                }
            }
        }
        
        stage('Create Version Tag') {
            when { 
                branch 'main' 
            }
            steps {
                script {
                    echo "Downloading and running GitVersion..."
                    
                    sh '''
                        curl -L https://github.com/GitTools/GitVersion/releases/download/6.4.0/gitversion-linux-x64-6.4.0.tar.gz -o gitversion.tar.gz
                        tar -xzf gitversion.tar.gz
                        chmod +x gitversion
                        ./gitversion -showvariable SemVer > version.txt
                    '''

                    env.MAIN_TAG = readFile('version.txt').trim()
                    sh 'rm -f gitversion* version.txt'
                    
                    echo "Version calculated: ${env.MAIN_TAG}"
                }
            }
        }
        
        stage('Push to ECR') {
            when { 
                branch 'main'
            }
            steps {
                script {
                    if (!env.MAIN_TAG || env.MAIN_TAG == '' || env.MAIN_TAG == 'null') {
                        error("env.MAIN_TAG is empty, null, or invalid: '${env.MAIN_TAG}'")
                    }
                    
                    echo "Pushing ${env.MAIN_TAG} to ECR..."
                    
                    sh '''
                        aws ecr get-login-password --region "${AWS_REGION}" | \
                            docker login --username AWS --password-stdin "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
                        
                        docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_REPO_BACKEND}:${MAIN_TAG}"
                        docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_REPO_BACKEND}:latest"
                        docker push "${ECR_REPO_BACKEND}:${MAIN_TAG}"
                        docker push "${ECR_REPO_BACKEND}:latest"
                    '''
                    
                    echo "Successfully pushed ${env.MAIN_TAG} to ECR"
                }
            }
        }
        
        stage('Deploy via GitOps') {
            when { 
                branch 'main' 
            }
            steps {
                script {
                    if (!env.MAIN_TAG || env.MAIN_TAG == '') {
                        echo "WARNING: env.MAIN_TAG not set, skipping GitOps update"
                        return
                    }
                    
                    sshagent(['github']) {
                        sh '''
                            rm -rf gitops-config
                            echo "Cloning GitOps repository..."
                            git clone "${GITOPS_REPO}" gitops-config
                        '''
                        
                        withCredentials([
                            string(credentialsId: 'git-username', variable: 'GIT_USERNAME'),
                            string(credentialsId: 'git-email', variable: 'GIT_EMAIL')
                        ]) {
                            dir('gitops-config') {
                                sh '''
                                    git config user.email "${GIT_EMAIL}"
                                    git config user.name "${GIT_USERNAME}"

                                    sed -i '/^  images:/,/^[^ ]/ s/backend: ".*"/backend: "'${MAIN_TAG}'"/' "${HELM_VALUES_PATH}"
                                    
                                    if git diff --quiet "${HELM_VALUES_PATH}"; then
                                        echo "No changes to deploy - version ${MAIN_TAG} already deployed"
                                    else
                                        git add "${HELM_VALUES_PATH}"
                                        git commit -m "Deploy backend v${MAIN_TAG} - Build ${BUILD_NUMBER}"
                                        git push origin main
                                        echo "GitOps updated: ${MAIN_TAG}"
                                    fi
                                '''
                            }
                        }
                    }
                }
            }
        }
        
        stage('Push Git Tag') {
            when { 
                branch 'main'
            }
            steps {
                script {
                    if (!env.MAIN_TAG || env.MAIN_TAG == '') {
                        echo "WARNING: env.MAIN_TAG not set, skipping git tag"
                        return
                    }
                    
                    echo "Pushing tag ${env.MAIN_TAG} to repository..."
                    
                    sshagent(['github']) {
                        withCredentials([
                            string(credentialsId: 'git-username', variable: 'GIT_USERNAME'),
                            string(credentialsId: 'git-email', variable: 'GIT_EMAIL')
                        ]) {
                            sh '''
                                git config user.email "${GIT_EMAIL}"
                                git config user.name "${GIT_USERNAME}"
                                
                                git tag -a "${MAIN_TAG}" -m "Release ${MAIN_TAG} - Build ${BUILD_NUMBER}"
                                git push origin "${MAIN_TAG}"
                            '''
                        }
                    }
                    
                    echo "Tag ${env.MAIN_TAG} pushed successfully"
                }
            }
        }
    }
    
    post {
        always {
            sh '''
                docker compose -f docker-compose.e2e.yml down -v || true
                rm -rf gitops-config || true
                docker rmi "${APP_NAME}:test-${BUILD_NUMBER}" || true
                docker rmi "${APP_NAME}:e2e-${BUILD_NUMBER}" || true

                docker image prune -f || true
                rm -f .env
            '''
            cleanWs()
        }
        success {
            echo 'Backend pipeline completed successfully!'
        }
        failure {
            echo 'Backend pipeline failed!'
        }
    }
}