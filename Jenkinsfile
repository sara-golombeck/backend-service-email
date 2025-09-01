

// // CURRENT VERSION WITH E2E TESTS AND GITVERSION
// pipeline {
//     agent any
    
//     environment {
//         APP_NAME = 'automarkly-backend'
//         BUILD_NUMBER = "${env.BUILD_NUMBER}"
//         AWS_ACCOUNT_ID = credentials('aws-account-id')
//         AWS_REGION = credentials('aws_region')
        
//         // Staging ECR
//         ECR_STAGING_BACKEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/staging/emailservice-backend"
        
//         // Production ECR
//         ECR_PROD_BACKEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-backend"
//         ECR_PROD_FRONTEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-frontend"
//         ECR_PROD_WORKER = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-worker"
        
//         GITOPS_REPO = 'git@github.com:sara-golombeck/gitops-email-service.git'
//         HELM_VALUES_PATH = 'charts/email-service/values.yaml'
//     }
    
//     triggers {
//         githubPush()
//     }
    
//     stages {
//         stage('Checkout') {
//             steps {
//                 checkout scm
//                 sshagent(['github']) {
//                     sh "git fetch --tags --unshallow || git fetch --tags"
//                 }
//             }
//         }
        
//         // stage('Unit Tests') {
//         //     steps {
//         //         sh '''
//         //             docker build -f Dockerfile.test -t "${APP_NAME}:test-${BUILD_NUMBER}" .
//         //             mkdir -p test-results
//         //             docker run --rm \
//         //                 -v "${PWD}/test-results:/src/test-results" \
//         //                 "${APP_NAME}:test-${BUILD_NUMBER}"
//         //         '''
//         //     }
//         //     post {
//         //         always {
//         //             archiveArtifacts artifacts: 'test-results/**/*', allowEmptyArchive: true
//         //         }
//         //     }
//         // }
        
//         stage('Build & Push to Staging') {
//             steps {
//                 sh '''
//                     aws ecr get-login-password --region "${AWS_REGION}" | \
//                         docker login --username AWS --password-stdin "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
                    
//                     docker build -t "${APP_NAME}:${BUILD_NUMBER}" .
//                     docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"
//                     docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_STAGING_BACKEND}:latest"
//                     docker push "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"
//                     docker push "${ECR_STAGING_BACKEND}:latest"
//                 '''
//             }
//         }
        
//         stage('Run E2E Tests') {
//             steps {
//                 build job: 'e2e-email-service',
//                       parameters: [
//                           string(name: 'BACKEND_IMAGE', value: "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"),
//                           string(name: 'FRONTEND_IMAGE', value: "${ECR_PROD_FRONTEND}:latest"),
//                           string(name: 'WORKER_IMAGE', value: "${ECR_PROD_WORKER}:latest")
//                       ],
//                       wait: true
//             }
//         }
        
//         stage('Create Version Tag') {
//             when { 
//                 allOf {
//                     branch 'main'
//                     expression { currentBuild.result == null || currentBuild.result == 'SUCCESS' }
//                 }
//             }
//             steps {
//                 script {
//                     sh '''
//                         curl -L https://github.com/GitTools/GitVersion/releases/download/6.4.0/gitversion-linux-x64-6.4.0.tar.gz -o gitversion.tar.gz
//                         tar -xzf gitversion.tar.gz
//                         chmod +x gitversion
//                         ./gitversion -showvariable SemVer > version.txt
//                     '''
//                     env.MAIN_TAG = readFile('version.txt').trim()
//                     sh 'rm -f gitversion* version.txt'
//                 }
//             }
//         }
        
//         stage('Promote to Production') {
//             when { 
//                 allOf {
//                     branch 'main'
//                     expression { currentBuild.result == null || currentBuild.result == 'SUCCESS' }
//                 }
//             }
//             steps {
//                 sh '''
//                     docker pull "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"
//                     docker tag "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}" "${ECR_PROD_BACKEND}:${MAIN_TAG}"
//                     docker tag "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}" "${ECR_PROD_BACKEND}:latest"
//                     docker push "${ECR_PROD_BACKEND}:${MAIN_TAG}"
//                     docker push "${ECR_PROD_BACKEND}:latest"
//                 '''
//             }
//         }
        
//         stage('Deploy via GitOps') {
//             when { 
//                 allOf {
//                     branch 'main'
//                     expression { currentBuild.result == null || currentBuild.result == 'SUCCESS' }
//                 }
//             }
//             steps {
//                 sshagent(['github']) {
//                     sh '''
//                         rm -rf gitops-config
//                         git clone "${GITOPS_REPO}" gitops-config
//                     '''
                    
//                     withCredentials([
//                         string(credentialsId: 'git-username', variable: 'GIT_USERNAME'),
//                         string(credentialsId: 'git-email', variable: 'GIT_EMAIL')
//                     ]) {
//                         dir('gitops-config') {
//                             sh '''
//                                 git config user.email "${GIT_EMAIL}"
//                                 git config user.name "${GIT_USERNAME}"

//                                 sed -i '/^  images:/,/^[^ ]/ s/backend: ".*"/backend: "'${MAIN_TAG}'"/' "${HELM_VALUES_PATH}"
                                
//                                 if git diff --quiet "${HELM_VALUES_PATH}"; then
//                                     echo "No changes to deploy"
//                                 else
//                                     git add "${HELM_VALUES_PATH}"
//                                     git commit -m "Deploy backend v${MAIN_TAG} - Build ${BUILD_NUMBER}"
//                                     git push origin main
//                                 fi
//                             '''
//                         }
//                     }
//                 }
//             }
//         }
//     }
    
//     post {
//         always {
//             sh '''
//                 docker rmi "${APP_NAME}:test-${BUILD_NUMBER}" || true
//                 docker rmi "${APP_NAME}:${BUILD_NUMBER}" || true
//                 docker image prune -f || true
//                 rm -rf gitops-config || true
//             '''
//             cleanWs()
//         }
//         success {
//             echo 'Backend pipeline completed successfully!'
//         }
//         failure {
//             echo 'Backend pipeline failed!'
//         }
//     }
// }

pipeline {
   agent any
   
   environment {
       APP_NAME = 'automarkly-backend'
       BUILD_NUMBER = "${env.BUILD_NUMBER}"
       AWS_ACCOUNT_ID = credentials('aws-account-id')
       AWS_REGION = credentials('aws_region')
       TEST_EMAIL = 'sara.beck.dev@gmail.com'
       
       // Staging ECR
       ECR_STAGING_BACKEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/staging/emailservice-backend"
       
       // Production ECR
       ECR_PROD_BACKEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-backend"
       ECR_PROD_FRONTEND = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-frontend"
       ECR_PROD_WORKER = "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/automarkly/emailservice-worker"
       
       GITOPS_REPO = credentials('gitops-repo-url')
       HELM_VALUES_PATH = 'charts/email-service/values.yaml'
   }
   
   triggers {
       githubPush()
   }
   
   stages {
       stage('Checkout') {
           when {
               anyOf {
                   branch 'main'
                   branch 'feature/*'
                   branch 'release/*'
               }
           }
           steps {
               checkout scm
               sshagent(['github']) {
                   sh "git fetch --tags --unshallow || git fetch --tags"
               }
           }
       }
       
       stage('Unit Tests') {
           when {
               anyOf {
                   branch 'main'
                   branch 'feature/*'
                   branch 'release/*'
               }
           }
           steps {
               sh '''
                   docker build -f Dockerfile.test -t "${APP_NAME}:test-${BUILD_NUMBER}" .
                   mkdir -p test-results
                   docker run --rm \
                       -v "${PWD}/test-results:/src/test-results" \
                       "${APP_NAME}:test-${BUILD_NUMBER}"
               '''
           }
           post {
               always {
                   archiveArtifacts artifacts: 'test-results/**/*', allowEmptyArchive: true
               }
           }
       }
       
       stage('Package') {
           when {
               anyOf {
                   branch 'main'
                   branch 'feature/*'
                   branch 'release/*'
               }
           }
           steps {
               timeout(time: 10, unit: 'MINUTES') {
                   sh '''
                       docker build -t "${APP_NAME}:${BUILD_NUMBER}" .
                       docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"
                   '''
               }
           }
       }
       
       stage('Push to Staging') {
           when {
               anyOf {
                   branch 'main'
                   branch 'feature/*'
                   branch 'release/*'
               }
           }
           steps {
               retry(3) {
                   sh '''
                       aws ecr get-login-password --region "${AWS_REGION}" | \
                           docker login --username AWS --password-stdin "${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
                   '''
               }
               
               timeout(time: 5, unit: 'MINUTES') {
                   sh '''
                       docker push "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"
                   '''
               }
           }
       }
       
       stage('Run E2E Tests') {
           when {
               anyOf {
                   branch 'main'
                   branch 'feature/*'
                   branch 'release/*'
               }
           }
           steps {
               build job: 'e2e-email-service',
                     parameters: [
                         string(name: 'BACKEND_IMAGE', value: "${ECR_STAGING_BACKEND}:${BUILD_NUMBER}"),
                         string(name: 'FRONTEND_IMAGE', value: "${ECR_PROD_FRONTEND}:latest"),
                         string(name: 'WORKER_IMAGE', value: "${ECR_PROD_WORKER}:latest"),
                         string(name: 'TEST_EMAIL', value: "${TEST_EMAIL}")
                     ],
                     wait: true
           }
       }
       
       stage('Create Version Tag') {
           when { 
               anyOf {
                   branch 'main'
                   branch 'release/*'
               }
           }
           steps {
               script {
                   sh '''
                       curl -L https://github.com/GitTools/GitVersion/releases/download/6.4.0/gitversion-linux-x64-6.4.0.tar.gz -o gitversion.tar.gz
                       tar -xzf gitversion.tar.gz
                       chmod +x gitversion
                       ./gitversion -showvariable SemVer > version.txt
                   '''
                   env.MAIN_TAG = readFile('version.txt').trim()
                   sh 'rm -f gitversion* version.txt'
               }
           }
       }
       
       stage('Promote to Production') {
           when { 
               anyOf {
                   branch 'main'
                   branch 'release/*'
               }
           }
           steps {
               retry(3) {
                   sh '''
                       docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_PROD_BACKEND}:${MAIN_TAG}"
                       docker tag "${APP_NAME}:${BUILD_NUMBER}" "${ECR_PROD_BACKEND}:latest"
                       docker push "${ECR_PROD_BACKEND}:${MAIN_TAG}"
                       docker push "${ECR_PROD_BACKEND}:latest"
                   '''
               }
           }
       }
       
       stage('Deploy via GitOps') {
           when { 
               anyOf {
                   branch 'main'
                   branch 'release/*'
               }
           }
           steps {
               sshagent(['github']) {
                   sh '''
                       rm -rf gitops-config
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
                                   echo "No changes to deploy"
                               else
                                   git add "${HELM_VALUES_PATH}"
                                   git commit -m "Deploy backend v${MAIN_TAG} - Build ${BUILD_NUMBER}"
                                   git push origin main
                               fi
                           '''
                       }
                   }
               }
           }
       }
   }
   
   post {
       always {
           script {
               def status = currentBuild.result ?: 'SUCCESS'
               
               emailext(
                   to: "${TEST_EMAIL}",
                   subject: "${APP_NAME} Build #${BUILD_NUMBER} - ${status}",
                   body: "Pipeline ${status}\nBuild: #${BUILD_NUMBER}\nDuration: ${currentBuild.durationString}\n\n${BUILD_URL}"
               )
           }
           
           sh '''
               docker rmi "${APP_NAME}:test-${BUILD_NUMBER}" || true
               docker rmi "${APP_NAME}:${BUILD_NUMBER}" || true
               docker image prune -f || true
               rm -rf gitops-config || true
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