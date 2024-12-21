# WIP

## Basic Info
The first user created will automatically be the SysAdmin user with the ID 0.
Creating the first user will also automatically create the SysAdmin role with the ID 0.

Once a user is created they can log in.
On the login call they will receive two cookies. An "auth" jwt and "refresh" token.
They work with the conventional jwt bearer scheme and can be configured in the config.json.


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

## Basic API Functionality
The [Authorize] attribute makes sure that the user has a valid JWT token.
To further authorize an endpoint with permissions metadata can be added to the EndpointBuilder like this .WithMetadata(new RequiredPermissionAttribute(Permissions.Roles.Create)
Examples for both can be found in the RoleEndpoint.cs
