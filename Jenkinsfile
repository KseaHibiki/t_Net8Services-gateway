pipeline {
    agent any

    environment {
        REGISTRY = 'localhost:5005'
        IMAGE_NAME = 'gateway'
        IMAGE_TAG = "${env.BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps {
                dir('t_Net8Services') {
                    checkout([
                        $class: 'GitSCM',
                        branches: [[name: '*/main']],
                        userRemoteConfigs: [[
                            url: 'https://github.com/KseaHibiki/t_Net8Services-gateway.git',
                            credentialsId: 'github-pat-token'
                        ]],
                        extensions: [[$class: 'RelativeTargetDirectory', relativeTargetDir: 'gateway']]
                    ])
                }
            }
        }

        stage('Restore & Build') {
            steps {
                dir('t_Net8Services') {
                    bat 'dotnet restore gateway/Gateway.csproj'
                    bat 'dotnet build gateway/Gateway.csproj -c Release --no-restore'
                }
            }
        }

        stage('Docker Build & Push') {
            steps {
                dir('t_Net8Services') {
                    bat "docker build -f gateway/Dockerfile -t ${REGISTRY}/${IMAGE_NAME}:%IMAGE_TAG% -t ${REGISTRY}/${IMAGE_NAME}:latest ."
                    bat "docker push ${REGISTRY}/${IMAGE_NAME}:%IMAGE_TAG%"
                    bat "docker push ${REGISTRY}/${IMAGE_NAME}:latest"
                }
            }
        }
    }

    post {
        success {
            echo "✅ Gateway 镜像已推送: ${REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"
        }
        failure {
            echo '❌ Gateway 构建失败'
        }
        always {
            cleanWs()
        }
    }
}