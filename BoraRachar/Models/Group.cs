using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BoraRachar.Models
{
    public class Group
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string? AccessTokenHash { get; set; }
        public string Name { get; set; } = null!;
        public List<Member> Members { get; set; } = new();
        public List<Expense> Expenses { get; set; } = new();
    }
}
