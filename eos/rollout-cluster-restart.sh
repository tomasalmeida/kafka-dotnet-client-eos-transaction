#!/bin/bash

echo "restarting kafka"
#docker-compose stop kafka-1 kafka-2 kafka-3
#docker-compose up -d kafka-1 kafka-2 kafka-3

rollout() {
  server=$1
  echo "rolling $server"
  docker-compose stop $server
  sleep 2
  docker-compose start $server
  URP=''
  while [[ $URP != "0" ]]; do
    URP=$(docker-compose exec kafka-1 kafka-topics --bootstrap-server kafka-1:9092,kafka-2:9092,kafka-3:9092 --describe --under-replicated-partitions | wc -l | awk '{print $1}')
    if [[ $URP != "0" ]]; then
      sleep 2
      echo "Waiting under replication partitions equals zero..."
    fi
  done
}
rollout kafka-1
rollout kafka-2
rollout kafka-3