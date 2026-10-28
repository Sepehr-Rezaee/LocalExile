using System.Text.Json;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

[Collection(TestSaves.FailWriteCollection)]
public class OpeningTutorialTests : IDisposable
{
    readonly string folder=TestSaves.Catalog(new TestSaves.Type(123,"Gem"));
    readonly string character="Beach"+Guid.NewGuid().ToString("N")[..12];
    static Item Gem => new(123,["Base"],1);
    public OpeningTutorialTests()=>TestSaves.Ensure();
    public void Dispose(){Saves.FailWrite=null;Directory.Delete(folder,true);}
    PlayerInventories Inventory()=>PlayerInventories.For(character,new ItemCatalog(folder));
    CharacterSaves.OpeningTutorial Tutorial()=>new(CharacterSaves.ToSaved(Gem),CharacterSaves.ToSaved(Gem),Id:Guid.NewGuid().ToString("N"));
    [Fact] public void OnlyAnUntouchedFirstBeachArrivalStartsLyingDown()
    {
        var tutorial=Tutorial();
        Assert.True(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand,tutorial,true));
        Assert.False(TutorialObjects.ShouldStartWashedUp("1_1_town",tutorial,true));
        Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand,null,true));
        Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand,tutorial,false));
        foreach(var progressed in new[] { tutorial with { WakeUpShown=true }, tutorial with { WeaponPromptHeard=true },
            tutorial with { Talked=true }, tutorial with { ZombieKilled=true }, tutorial with { ChestOpened=true },
            tutorial with { SkillClaimed=true }, tutorial with { SupportClaimed=true } })
            Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand,progressed,true));
        CharacterSaves.SetTutorial(character,tutorial);
        CharacterSaves.ChangeTutorial(character,t=>t with { WakeUpShown=true });
        Assert.False(TutorialObjects.ShouldStartWashedUp(TutorialObjects.Strand,CharacterSaves.Load(character)!.Tutorial,true));
    }
    [Fact] public void FittedStartingRoomsKeepTheirSpawnPointsFacingDirection()
    {
        const string room="Metadata/Terrain/Act1/Area1/Rooms/washed_up3.arm";
        Directory.CreateDirectory(Path.Combine(folder,"walkability"));
        File.WriteAllText(Path.Combine(folder,"room-points.json"),JsonSerializer.Serialize(new
        {
            areas=new Dictionary<string,string[]> { [TutorialObjects.Strand]=[room] },
            rooms=new Dictionary<string,object> { [room]=new
            {
                points=new[] { new { name="default",x=10,y=20,angle=MathF.PI*1.5f } },
                doodads=new[] { new { model="A",x=0,y=0 },new { model="B",x=7,y=3 },
                    new { model="C",x=11,y=13 },new { model="D",x=2,y=17 } },
            } },
        }));
        File.WriteAllText(Path.Combine(folder,"walkability","1_1_1_seed1.doodads.json"),
            """{"models":["A","B","C","D"],"doodads":[[0,100,200],[1,103,193],[2,113,189],[3,117,198]]}""");
        var start=Assert.Single(AreaPopulation.FitRooms(TutorialObjects.Strand,1,[],folder).Points);
        Assert.Equal((120,190),(start.X,start.Y));
        Assert.Equal(MathF.PI,start.Angle,5);
    }
    [Fact] public void UnarmedDialogueWaitsForAWeaponAndArmedDialogueUsesItsOwnVoiceLength()
    {
        Assert.Equal(((byte)1, (double?)null), TutorialObjects.Dialogue(false, false));
        Assert.Equal(((byte)1, (double?)null), TutorialObjects.Dialogue(false, true));
        Assert.Equal(((byte)2, (double?)22.8), TutorialObjects.Dialogue(true, false));
        Assert.Equal(((byte)3, (double?)8.2), TutorialObjects.Dialogue(true, true));
    }
    [Fact] public void HearingTheWeaponPromptSurvivesReloadWithoutStartingTheZombieScene()
    {
        var original=Tutorial();
        CharacterSaves.SetTutorial(character,original);
        CharacterSaves.ChangeTutorial(character,t=>t with { WeaponPromptHeard=true });
        var loaded=CharacterSaves.Load(character)!.Tutorial!;
        Assert.True(loaded.WeaponPromptHeard);
        Assert.False(loaded.Talked);
        Assert.False(loaded.ZombieKilled);
        Assert.Equal(original.Id,loaded.Id);
        Assert.Equal((byte)3,TutorialObjects.Dialogue(true,loaded.WeaponPromptHeard).Option);
        // Saves written before this field existed continue with the original introductory voice.
        var old=JsonSerializer.Deserialize<CharacterSaves.OpeningTutorial>("""{"SkillGem":{"Type":123},"SupportGem":{"Type":123}}""")!;
        Assert.False(old.WeaponPromptHeard);
        Assert.Equal((byte)2,TutorialObjects.Dialogue(true,old.WeaponPromptHeard).Option);
    }
    [Fact] public void GemsRequireTheirEncounterAndCanOnlyBeClaimedOnceAcrossReloads()
    {
        var inventory=Inventory(); CharacterSaves.SetTutorial(character,Tutorial());
        InventoryItem Put()=>inventory.Add(0,0,0,Gem);
        Assert.Null(TutorialObjects.Claim(character,false,Gem,Put));
        CharacterSaves.ChangeTutorial(character,t=>t with { ZombieKilled=true });
        Assert.NotNull(TutorialObjects.Claim(character,false,Gem,Put));
        PlayerInventories.Forget(character);inventory=Inventory();
        Assert.Null(TutorialObjects.Claim(character,false,Gem,Put));
        Assert.Null(TutorialObjects.Claim(character,true,Gem,Put));
        CharacterSaves.ChangeTutorial(character,t=>t with { ChestOpened=true });
        Assert.NotNull(TutorialObjects.Claim(character,true,Gem,()=>inventory.Add(0,1,0,Gem)));
        var state=CharacterSaves.Load(character)!.Tutorial!;
        Assert.True(state.SkillClaimed && state.SupportClaimed);Assert.Equal(2,inventory.Items(0).Count);
    }
    [Fact] public void FailedClaimKeepsBothGemAndPendingState()
    {
        var inventory=Inventory();CharacterSaves.SetTutorial(character,Tutorial() with {ZombieKilled=true});
        int writes=0;Saves.FailWrite=key=>key==character&&++writes==2;
        Assert.ThrowsAny<Exception>(()=>TutorialObjects.Claim(character,false,Gem,()=>inventory.Add(0,0,0,Gem)));
        Saves.FailWrite=null;PlayerInventories.Forget(character);
        Assert.Empty(Inventory().Items(0));Assert.False(CharacterSaves.Load(character)!.Tutorial!.SkillClaimed);
    }
    [Fact] public void ChestUsesCapturedPlacementOnlyForItsAreaAndSeed()
    {
        string file=Path.Combine(folder,"1_1_1_seed7.objects.json");
        void Write(string area,int seed)=>File.WriteAllText(file,JsonSerializer.Serialize(new {area,seed,objects=new[]{new {objectType=TutorialObjects.Chest,model="Metadata/Test/Chest.ao",x=123,y=456,rotation=1.25}}}));
        Write("1_1_1",7);var chest=TutorialObjects.ChestPlacement("1_1_1",7,folder)!;
        Assert.Equal((123,456),(chest.X,chest.Y));Assert.Equal(1.25f,chest.Rotation);
        Write("wrong-area",7);Assert.Null(TutorialObjects.ChestPlacement("1_1_1",7,folder));
        Write("1_1_1",8);Assert.Null(TutorialObjects.ChestPlacement("1_1_1",7,folder));
    }
    [Fact] public void GroundRollbackKeepsTheOriginalClaimId()
    {
        var world=new InstanceState();var item=new WorldItemObject(1,2,Gem);
        uint id=world.PlaceTutorialItem("character/skill",item)!.Value;
        Assert.Null(world.PlaceTutorialItem("character/skill",item));
        world.Ground.Take(id);world.Ground.Restore(id,item);
        Assert.True(world.IsStartWeapon("character/skill",id));Assert.Same(item,world.Ground.Get(id));
    }
    [Fact] public void NamedChestUsesCapturedRoomAndRejectsAnotherAreaOrSeed()
    {
        string file=Path.Combine(folder,"1_1_1_seed1.rooms.json"),path="Metadata/Terrain/Act1/Area1/Rooms/tutorial_01.arm";
        void Write(string area,int seed)=>File.WriteAllText(file,JsonSerializer.Serialize(new {area,seed,rooms=new[]{new {path,x=759,y=483,width=138,height=207,code=0}}}));
        Write("1_1_1",1);
        var room=Assert.Single(TutorialObjects.RoomPlacements("1_1_1",1,path.ToUpperInvariant(),folder));
        var chest=room.Place(new(TutorialObjects.Chest,41,64,5.84685f));
        Assert.Equal((800,547),(chest.X,chest.Y));Assert.Equal(5.84685f,chest.Angle,5);
        // Independently captured Wounded Exile: washed_up3's room code 3 at (207,138), local (98,122).
        var exile=new TutorialObjects.RoomPlacement(207,138,207,207,3).Place(new("exile",98,122));
        Assert.Equal((329,246),(exile.X,exile.Y));
        Write("wrong",1);Assert.Empty(TutorialObjects.RoomPlacements("1_1_1",1,path,folder));
        Write("1_1_1",2);Assert.Empty(TutorialObjects.RoomPlacements("1_1_1",1,path,folder));
    }
    [Fact] public void ActualOpeningZombieResolvesItsVarietyRatherThanItsBaseObjectType()
    {
        if(!File.Exists("data/tables/MonsterVarieties.json")) return;
        var zombie=Assert.IsType<MonsterCatalog.Variety>(new MonsterCatalog().Find(TutorialObjects.Zombie));
        Assert.True(zombie.Supported);
        Assert.Equal("Metadata/Monsters/Zombies/Drowned/BiteZombie",zombie.BaseType);
        if(File.Exists("data/monster-takehit.json"))
            Assert.Equal(TutorialObjects.BiteSeconds,new MonsterEmerge().Seconds(zombie));
    }
}
