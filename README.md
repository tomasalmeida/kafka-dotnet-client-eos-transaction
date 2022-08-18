# kafka-dotnet-client-eos-transaction
Simple example of a .net consumer/producer EOS transaction

## How to run

```shell
    ./start-cluster.sh
```

In another terminal

```shell
    ./start-consumer.sh 
```

It is interesting to see the client logs during the process

```shell
    docker-compose logs -f eos
```


Rollout the cluster/Users/talmeida/Documents/code/my-repos/kafka-dotnet-client-eos-transaction/docker-compose

```shell
    ./rollout-cluster-restart.sh
```

### Relaunch EOS for tests

```shell
    ./restart-eos.sh
```


## clean up

1. Stop all console-consumer/producer
2. Stop the start.shÍÍ
