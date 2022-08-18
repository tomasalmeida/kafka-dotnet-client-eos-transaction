#!/bin/bash
docker-compose kill eos 
docker-compose build 
docker-compose up -d eos
docker-compose logs -f eos
