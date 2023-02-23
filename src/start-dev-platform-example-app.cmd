docker network create platform-example-app-network

docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example kill
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build sql-data
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build mongo-data
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build postgres-sql
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build rabbitmq
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build redis-cache
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build text-snippet-api
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example build text-snippet-webspa
docker-compose -f platform-example-app.docker-compose.yml -f platform-example-app.docker-compose.override.yml -p easyplatform-example up --remove-orphans --detach
pause
