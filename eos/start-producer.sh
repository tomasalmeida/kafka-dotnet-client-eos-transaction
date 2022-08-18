#!/bin/bash
kafka-console-producer --broker-list kafka-1:19092,kafka-2:29092,kafka-3:39092 --topic topic-test --property parse.key=true --property key.separator=,