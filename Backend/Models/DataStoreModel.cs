using Backend.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models;

public class DataStoreModel
{
    [BsonId]
    public required EDataStore DataStoreType { get; set; }
    public required long Value { get; set; }
}