#!/bin/bash

# Build localy docker-compose images
docker-compose down -v
docker-compose build

# Start kafka brokers & zookeeper
docker-compose up -d zookeeper-1 kafka-1 kafka-2 kafka-3

# Wait zookeeper-1 is UP
ZOOKEEPER_STATUS=""
while [[ $ZOOKEEPER_STATUS != "imok" ]]; do
  echo "Waiting zookeeper UP..."
  sleep 1
  ZOOKEEPER_STATUS=$(echo ruok | docker-compose exec zookeeper-1 nc localhost 2181)
done
echo "Zookeeper ready!!"

# Wait brokers is UP
FOUND=''
while [[ $FOUND != "yes" ]]; do
  echo "Waiting kafka UP..."
  sleep 1
  FOUND=$(docker-compose exec zookeeper-1 zookeeper-shell zookeeper-1 get /brokers/ids/101 &>/dev/null && echo 'yes')
done

# Create topic which use by producer & consumer
docker-compose exec kafka-1 kafka-topics --bootstrap-server kafka-1:19092 --topic topic-test       --create --partitions 4 --replication-factor 3
docker-compose exec kafka-1 kafka-topics --bootstrap-server kafka-1:19092 --topic topic-test-final --create --partitions 4 --replication-factor 3
echo "Topic created"

docker-compose up -d eos
watch -n1 docker-compose ps -a
