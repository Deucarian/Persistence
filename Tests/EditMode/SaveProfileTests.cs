using System;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Deucarian.Persistence.Tests
{
    public sealed class SaveProfileTests
    {
        [Test]
        public async Task ProfilesRouteTypedDefinitionsToIndependentSlots()
        {
            var definition = new DocumentDefinition<Data>(new DocumentId("settings"), new SchemaVersion(1), () => new Data());
            using (var service = new PersistenceService(new InMemoryTextStorage()))
            using (var a = new SaveProfile(service, new SaveSlotId("a")))
            using (var b = new SaveProfile(service, new SaveSlotId("b")))
            {
                a.Register(definition); b.Register(definition);
                Assert.That((await a.SaveAsync("settings", new Data { Value = 5 })).Succeeded, Is.True);
                Assert.That((await b.LoadAsync<Data>("settings")).Document.Value, Is.Zero);
                Assert.That((await a.LoadAsync<Data>("settings")).Document.Value, Is.EqualTo(5));
                Assert.Throws<InvalidOperationException>(() => a.LoadAsync<string>("settings"));
                a.Dispose();
                Assert.That((await b.SaveAsync("settings", new Data())).Succeeded, Is.True);
            }
        }
        public sealed class Data { public int Value; }
    }
}
