# WIP



## Setting up MongoDB

1. Start MongoDB with repliction enabled.
  -  docker run -d --name mongodb -p 8000:27017 mongo --replSet rs0
2. Initiate rs0 in mongosh
  - rs.initiate({
    _id: "rs0",
    members: [
        { _id: 0, host: "localhost:27017" }
    ]
})
