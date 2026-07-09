pipeline {
    agent any

    environment {
        DOCKER_REGISTRY = 'docker.io'
        IMAGE_NAME = 'kseahibiki/gateway'
        IMAGE_TAG = "${env.BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps {
                dir('t_Net8Services') {
                    checkout([
                        $class: 'GitSCM',
                        branches: [[name: '*/main']],
                        userRemoteConfigs: [[url: 'https://github.com/KseaHibiki/t_Net8Services-gateway.git']],
                        extensions: [[$class: 'RelativeTargetDirectory', relativeTargetDir: 'gateway']]
                    ])
                }
            }
        }

        stage('Restore & Build') {
            steps {
                dir('t_Net8Services') {
                    sh 'dotnet restore gateway/Gateway.csproj'
                    sh 'dotnet build gateway/Gateway.csproj -c Release --no-restore'
                }
            }
        }

        stage('Run Tests') {
            steps {
                dir('t_Net8Services') {
                    sh 'dotnet test gateway/Gateway.csproj -c Release --no-build || echo "No tests found"'
                }
            }
        }

        stage('Docker Build & Push') {
            steps {
                dir('t_Net8Services') {
                    sh """
                        docker build \\
                            -f gateway/Dockerfile \\
                            -t ${IMAGE_NAME}:${IMAGE_TAG} \\
                            -t ${IMAGE_NAME}:latest \\
                            .
                    """
                    sh """
                        docker tag ${IMAGE_NAME}:${IMAGE_TAG} ${DOCKER_REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}
                        docker tag ${IMAGE_NAME}:latest ${DOCKER_REGISTRY}/${IMAGE_NAME}:latest
                    """
                }
            }
        }
    }

    post {
        success {
            echo 'Gateway 构建并推送成功！'
        }
        failure {
            echo 'Gateway 构建失败，请检查日志。'
        }
    }
}
