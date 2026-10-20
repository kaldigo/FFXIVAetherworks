using Aetherworks.Ownership;
using Aetherworks.Crafting;
using Aetherworks.Glamour;

internal static class RetainerCaptureTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Retainer capture: " + message); }
        var store = new OwnershipStore();
        store.ReplaceRetainers(new Dictionary<string,string> {
            [OwnershipStore.RetainerKey(1)]="First", [OwnershipStore.RetainerKey(2)]="Second" });
        var key = OwnershipStore.RetainerKey(1);
        var other = OwnershipStore.RetainerKey(2);
        var capture = new RetainerCapture();
        void Observe(long time, int count=0, int capacity=0, List<(uint ItemId,long Quantity)>? bags=null,
            List<(uint ItemId,long Quantity)>? crystals=null, ulong selected=1, ulong owner=1, bool visible=true, bool readable=true)
            => capture.Observe(store,selected,owner,visible,count,capacity,bags ?? [],readable,crystals,time);

        Observe(0); Observe(500);
        Check(!store.Snapshot.Locations.ContainsKey(key),"initial empty/transitional read committed");
        Observe(1000);
        Check(store.Snapshot.Locations[key].Count==0 && store.Dirty,"confirmed empty bags were not recorded");
        Check(store.TryGetQuantity(key,100,out var zero) && zero==0,"empty bag is still Missing");
        Check(!store.TryGetQuantity(key,2,out _),"unloaded crystals were incorrectly recorded as empty");
        Check(store.Find(100,[(key,"First")]).Count==0 && store.Find(2,[(key,"First")]).Single().Quantity is null,
            "empty and unknown parts are conflated in tooltips");
        var catalog = new AppearanceCatalog([]);
        Check(OwnershipTooltip.Compose(100,store,catalog,[(key,"First")],true,false).Count==0 &&
            OwnershipTooltip.Compose(2,store,catalog,[(key,"First")],true,false).SequenceEqual(["First: Missing"]),
            "native tooltip composition ignores independently known bags/crystals");
        var reload = new OwnershipStore(); reload.Load(store.Serialize());
        Check(reload.TryGetQuantity(key,100,out _) && !reload.TryGetQuantity(key,2,out _),"partial empty snapshot did not survive reload");
        store.MarkSaved(); Observe(1500);
        Check(!store.Dirty,"unchanged empty retainer causes endless saves");
        Observe(2000,crystals:[(2,123)]); Observe(3000,crystals:[(2,123)]);
        Check(store.TryGetQuantity(key,2,out var crystals) && crystals==123,"crystals did not load independently");
        Check(store.TryGetQuantity(key,100,out zero) && zero==0,"crystal capture lost known empty bags");

        // Every known occupied stack must be accounted for before a read can settle.
        Observe(4000,count:2,capacity:150,bags:[(100,2)]); Observe(6000,count:2,capacity:150,bags:[(100,2)]);
        Check(store.Snapshot.Locations[key].GetValueOrDefault(100u)==0,"partial loaded bags overwrote snapshot");
        Observe(6500,count:2,capacity:175,bags:[(100,2)]); Observe(8000,count:2,capacity:175,bags:[(100,2)]);
        Check(store.Snapshot.Locations[key].GetValueOrDefault(100u)==0,"stale roster item count was ignored");
        Observe(8500,count:2,capacity:175,bags:[(100,2),(100,3)]);
        Observe(9500,count:2,capacity:175,bags:[(100,2),(100,3)]);
        Check(store.Snapshot.Locations[key][100]==5 && store.Snapshot.Locations[key][2]==123,"stack aggregation or remembered crystals lost");
        // The metadata count proves unused/unloaded pages contain no additional stacks.
        Observe(9600,count:1,capacity:25,bags:[(100,9)]);
        Observe(10600,count:1,capacity:25,bags:[(100,9)]);
        Check(store.Snapshot.Locations[key][100]==9,"mostly empty retainer requires unused pages to load");
        Observe(11000,count:0,capacity:175,bags:[(100,5)]); Observe(12000,count:0,capacity:175,bags:[(100,5)]);
        Check(store.Snapshot.Locations[key][100]==9,"contradictory zero roster count replaced nonempty bags");
        Observe(12500,readable:false); Observe(14000,readable:false);
        Check(store.Snapshot.Locations[key][100]==9,"unreadable slots were mistaken for empty bags");
        Observe(14500); Observe(15500);
        Check(!store.Snapshot.Locations[key].ContainsKey(100) && store.Snapshot.Locations[key][2]==123,"last withdrawn item or retained crystals incorrect");
        Observe(16000,crystals:[]); Observe(17000,crystals:[]);
        Check(store.Snapshot.Locations[key].Count==0 && store.TryGetQuantity(key,2,out zero) && zero==0,"loaded empty crystals not recorded");

        // Switching two identical empty retainers still requires a new settled identity.
        Observe(18000,selected:2,owner:1); Observe(20000,selected:2,owner:1);
        Check(!store.Snapshot.Locations.ContainsKey(other),"mismatched identity accepted");
        Observe(21000,selected:2,owner:2); Observe(21500,selected:2,owner:2);
        Check(!store.Snapshot.Locations.ContainsKey(other),"previous retainer's settling timer leaked");
        Observe(22000,selected:2,owner:2);
        Check(store.TryGetQuantity(other,100,out zero) && zero==0 && !store.TryGetQuantity(other,2,out _),"second empty retainer not independently captured");
        Observe(23000,visible:false); Observe(24000,crystals:[(2,7)]); Observe(24500,crystals:[(2,7)]);
        Check(store.Snapshot.Locations[key].Count==0,"closing inventory did not invalidate pending observation");
        Observe(25000,crystals:[(2,7)]);
        Check(store.Snapshot.Locations[key][2]==7,"reopened inventory failed to resume");

        // Crystal availability must not be held hostage by incomplete ordinary bags.
        var separate = new OwnershipStore(); separate.ObserveRetainer(3,"Third");
        capture.Reset();
        capture.Observe(separate,3,3,true,2,25,[(100,1)],true,[(3,42)],0);
        capture.Observe(separate,3,3,true,2,25,[(100,1)],true,[(3,42)],1000);
        var third=OwnershipStore.RetainerKey(3);
        Check(separate.TryGetQuantity(third,3,out var q) && q==42 && !separate.TryGetQuantity(third,100,out _),"crystal-only capture misstates bag completeness");
        Check(separate.RetainerLocations().Any(l=>l.Id==third) && separate.RetainerLocations().Any(l=>l.Id=="retainers-unobserved"),
            "quickly opening a retainer either loses its name or claims a complete roster");
        separate.Replace("inventory",[]); separate.Replace("saddlebag",[]);
        var materials=RecipeMaterialOwnership.Find([new(3,"Crystal"),new(100,"Material")],separate);
        Check(materials.Single(m=>m.Name=="Crystal").OutsideQuantity==42 && materials.Single(m=>m.Name=="Material").HasMissingData,
            "crafting locations do not respect part coverage");
        separate.ReplaceRetainers(new Dictionary<string,string>());
        Check(!separate.Snapshot.Locations.ContainsKey(third) && !separate.Snapshot.RetainerCoverage.ContainsKey(third),"dismissed retainer coverage remains");

        // Old complete snapshots retain their quantities and can refresh one part at a time.
        var old=new OwnershipStore(); old.Load("{\"Version\":1,\"RetainersKnown\":true,\"Retainers\":{\"retainer:1\":\"First\"},\"Locations\":{\"retainer:1\":{\"100\":4,\"2\":99}}}");
        old.ReplaceRetainerPart(key,RetainerParts.Bags,[]);
        Check(old.TryGetQuantity(key,100,out zero) && zero==0 && old.TryGetQuantity(key,2,out q) && q==99,"legacy snapshot migration lost crystals");
        reload.Load(old.Serialize());
        Check(reload.TryGetQuantity(key,2,out q) && q==99,"refreshed legacy snapshot lost coverage on disk");
        var before=reload.Serialize();
        foreach(var malformed in new[]{"null","{\"retainer:1\":0}","{\"retainer:1\":8}","{\"retainer:missing\":1}"})
        {
            string json=before.Replace("\"RetainerCoverage\":{\"retainer:1\":3}","\"RetainerCoverage\":"+malformed);
            bool rejected=false;
            try { reload.Load(json); } catch(InvalidDataException) { rejected=true; }
            Check(rejected && reload.Serialize()==before,"invalid coverage replaced the last good snapshot");
        }
        return checks;
    }
}
