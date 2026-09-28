using BoraRachar.Data;
using BoraRachar.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BoraRachar.Security
{
    public static class GroupTokenAdmin
    {
        public static async Task<int> RotateAsync(IServiceProvider services, string id)
        {
            if (!ObjectId.TryParse(id, out var parsed)) { Console.Error.WriteLine("ID inválido."); return 1; }
            var settings = services.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var collection = services.GetRequiredService<IMongoClient>().GetDatabase(settings.DatabaseName).GetCollection<Group>("Groups");
            var token = GroupTokens.Generate();
            var result = await collection.UpdateOneAsync(g => g.Id == parsed.ToString(),
                Builders<Group>.Update.Set(g => g.AccessTokenHash, GroupTokens.Hash(token)));
            if (result.MatchedCount == 0) { Console.Error.WriteLine("Grupo não encontrado."); return 1; }
            Console.WriteLine("Token novo (guarde com segurança; o token anterior foi invalidado):");
            Console.WriteLine(token);
            return 0;
        }
    }
}
