using System;
using System.Collections.Generic;
using Masterplan.Data;
using Newtonsoft.Json;

namespace EncounterExport
{
    public class FoundryCreatureAndErrors
    {
        public FoundryCreature Creature { get; set; } = new FoundryCreature();
        public List<String> Errors { get; set; } = new List<string>();
        public bool HasError => Errors != null && Errors.Count > 0;
        public string Name => Creature.Name;
    }

    public class FoundryCreatureData
    {
        public string name { get; set; }
        public Abilities abilities { get; set; } = new Abilities();
        public Attributes attributes { get; set; } = new Attributes();
        public Defences defences { get; set; } = new Defences();
        public Details details { get; set; } = new Details();
        public IntValueHolder actionpoints { get; set; } = new IntValueHolder();
        public Movement movement { get; set; } = new Movement();
        public Skills skills { get; set; } = new Skills();
        public string biography { get; set; } = "";
        public Senses senses { get; set; } = new Senses();
        public Dictionary<string, DamageMod> resistances { get; set; } = new Dictionary<string, DamageMod>();
    }

    public class FoundryCreature
    {
        public string Name => Data.name;
        public FoundryCreatureData Data { get; set; } = new FoundryCreatureData();
        public FoundryTokenData Token { get; set; } = new FoundryTokenData();
        public List<FoundryPower> Powers { get; set; } = new List<FoundryPower>();
        public List<FoundryTrait> Traits { get; set; } = new List<FoundryTrait>();
        public object creature { get; set; }
        public object card { get; set; }
    }
    
    public class FoundryTokenData
    {
        public bool actorLink { get; set; } = false;
        public int displayBars { get; set; } = 40; //OWNER
        public Dictionary<string, object> flags { get; set; } = new Dictionary<string, object>();
    }

    public class FoundryTokenAuraData
    {
        public int distance { get; set; } = 1;
        public string colour { get; set; }
        public double opacity { get; set; } = 0.3;
        public bool square { get; set; } = true;
        public string permission { get; set; } = "all";
    }

    public class Movement
    {
        [JsonProperty("base")]
        public IntValueHolderWithBase baseValue { get; set; } = new IntValueHolderWithBase();
        public string notes { get; set; }
    }
    public class Abilities
    {
        public IntValueHolder str { get; set; } = new IntValueHolder();
        public IntValueHolder con { get; set; } = new IntValueHolder();
        public IntValueHolder dex { get; set; } = new IntValueHolder();
        [JsonProperty("int")]
        public IntValueHolder intValue { get; set; } = new IntValueHolder();
        public IntValueHolder wis { get; set; } = new IntValueHolder();
        public IntValueHolder cha { get; set; } = new IntValueHolder();
    }

    public class DamageMod : IntValueHolder
    {
        public bool immune { get; set; } = false;
        public List<Bonus> bonus { get; set; } = new List<Bonus>();
    }

    public class Skills
    {
        public IntValueHolder acr { get; set; } = new IntValueHolder();
        public IntValueHolder arc { get; set; } = new IntValueHolder();
        public IntValueHolder ath { get; set; } = new IntValueHolder();
        public IntValueHolder blu { get; set; } = new IntValueHolder();
        public IntValueHolder dip { get; set; } = new IntValueHolder();
        public IntValueHolder dun { get; set; } = new IntValueHolder();
        public IntValueHolder end { get; set; } = new IntValueHolder();
        public IntValueHolder hea { get; set; } = new IntValueHolder();
        public IntValueHolder his { get; set; } = new IntValueHolder();
        public IntValueHolder ins { get; set; } = new IntValueHolder();
        public IntValueHolder itm { get; set; } = new IntValueHolder();
        public IntValueHolder nat { get; set; } = new IntValueHolder();
        public IntValueHolder prc { get; set; } = new IntValueHolder();
        public IntValueHolder rel { get; set; } = new IntValueHolder();
        public IntValueHolder stl { get; set; } = new IntValueHolder();
        public IntValueHolder stw { get; set; } = new IntValueHolder();
        public IntValueHolder thi { get; set; } = new IntValueHolder();
    }

    public class IntValueHolder
    {
        public int value { get; set; }
    }

    public class IntValueHolderWithMax : IntValueHolder
    {
        public int max => value;
    }
    
    public class UsesPer
    {
        public int? value { get; set; }
        public int? max => value;
        public string per { get; set; }
    }

    public class IntValueHolderWithBase : IntValueHolder
    {
        [JsonProperty("base")]
        public int baseValue => value;
    }

    public class IntValueWithBonuses : IntValueHolder
    {
        public List<Bonus> bonus { get; set; } = new List<Bonus>();
    }

    public class Attributes
    {
        public IntValueHolderWithMax hp { get; set; } = new IntValueHolderWithMax();
        public IntValueWithBonuses init { get; set; } = new IntValueWithBonuses();
    }

    public class Defence : IntValueHolder
    {
        [JsonProperty("base")]
        public int baseValue => value;
    }

    public class Defences
    {
        public Defence ac { get; set; } = new Defence();
        public Defence fort { get; set; } = new Defence();
        [JsonProperty("ref")]
        public Defence refValue { get; set; } = new Defence();
        public Defence wil { get; set; } = new Defence();
    }

    public class Senses
    {
        public Sense vision { get; set; } = new Sense();
        public Sense special { get; set; } = new Sense();
    }

    public class Sense
    {
        public List<List<string>> value { get; set; } = new List<List<string>>();
        public string custom { get; set; }
    }

    public class Details
    {
        public string origin { get; set; }
        [JsonProperty("type")]
        public string typeValue { get; set; }
        public string other { get; set; }
        public int level { get; set; } = 1;
        public int exp { get; set; }
        public int bloodied { get; set; }
        public int surgeValue { get; set; }
        public string size { get; set; } = "med";
        public IntValueHolderWithMax surges { get; set; } = new IntValueHolderWithMax();
        public IntValueWithBonuses saves { get; set; } = new IntValueWithBonuses();
        public string alignment { get; set; }
        public Role role { get; set; } = new Role();
    }

    public class Role
    {
        public string primary { get; set; }
        public string secondary { get; set; }
        
        public bool leader { get; set; }
    }

    public class Bonus : IntValueHolder
    {
        public string name { get; set; }
        public bool active { get; set; } = true;
        public string note { get; set; }
    }

    public class FoundryTrait
    {
        public string name { get; set; }
        public string type { get; set; } = "raceFeats";
        public string img { get; set; } = "icons/svg/light.svg";
        public FoundryTraitData data { get; set; } = new FoundryTraitData();
    }

    public class FoundryTraitData
    {
        public FoundryPowerDescription description { get; set; } = new FoundryPowerDescription();
    }
    public class FoundryPower
    {
        public string _id { get; set; }
        public string name { get; set; }
        public string type { get; set; } = "power";
        public string img { get; set; } = "icons/svg/aura.svg";
        public FoundryPowerData data { get; set; } = new FoundryPowerData();
    }

    public class PowerComparer : IComparer<FoundryPower>
    {
        public int Compare(FoundryPower x, FoundryPower y)
        {
            if (x == null && y == null)
            {
                return 0;
            } 
            
            if (x == null)
            {
                return 1;
            }

            if (y == null)
            {
                return -1;
            }
            switch (x.data.attack.isAttack)
            {
                case true when !y.data.attack.isAttack:
                    return -1;
                case false when y.data.attack.isAttack:
                    return 1;
                case false when !y.data.attack.isAttack:
                    return string.Compare(x.name, y.name, StringComparison.Ordinal);
            }
            
            switch (x.data.basicAttack)
            {
                case true when !y.data.basicAttack:
                    return -1;
                case false when y.data.basicAttack:
                    return 1;
                case true:
                    switch (x.data.isMelee)
                    {
                        case true when !y.data.isMelee:
                            return -1;
                        case false when y.data.isMelee:
                            return 1;
                    }
                    return string.Compare(x.name, y.name, StringComparison.Ordinal);
            }
            return string.Compare(x.name, y.name, StringComparison.Ordinal);
        }
    }

    public class FoundryPowerData
    {
        public FoundryPowerDescription description { get; set; } = new FoundryPowerDescription();
        public string source { get; set; }
        
        public string level { get; set; } = "";
        public List<string> keywords { get; set; } = new List<string>();
        public string powersource { get; set; } = "";
        public string subName { get; set; }
        public bool prepared { get; set; } = true;
        public string powerType { get; set; } = "class";
        public bool basicAttack { get; set; } = false;
        public string useType { get; set; } = "atwill";
        public UsesPer uses { get; set; } = new UsesPer();
        public string actionType { get; set; } = "standard";
        //public string requirements { get; set; } = "";
        public string weaponType { get; set; } = "none";
        public string weaponUse { get; set; } = "none";
        public string rangeType { get; set; } = "";
        public bool isMelee { get; set; } = false;
        public string rangeTextShort { get; set; } = "";
        public string rangeText { get; set; } = "";
        public int rangePower { get; set; }
        public int area { get; set; } = 0;
        public int? rechargeRoll { get; set; }
        public string rechargeCondition { get; set; } = "";
        public bool damageShare { get; set; } = false;
        public bool postEffect { get; set; } = true;
        public bool postSpecial { get; set; } = true;
        public bool autoGenChatPowerCard { get; set; } = true;
        public FoundryPowerSustain sustain { get; set; } = new FoundryPowerSustain();
        
        public string target { get; set; }
        public FoundryPowerAttack attack { get; set; } = new FoundryPowerAttack();
        public FoundryPowerHit hit { get; set; } = new FoundryPowerHit();
        public FoundryPowerMiss miss { get; set; } = new FoundryPowerMiss();
        public FoundryPowerEffect effect { get; set; } = new FoundryPowerEffect();
        public string trigger { get; set; }
        public string requirement { get; set; }
        public string special { get; set; }
       public string chatFlavor { get; set; } = "";

       public Dictionary<string, bool> damageType = new Dictionary<string, bool>();
       
       public Dictionary<string, bool> effectType = new Dictionary<string, bool>();
    }

    public class FoundryPowerDescription
    {
        public string value { get; set; } = "";
        public string chat { get; set; } = "";
        public string unidentified { get; set; } = "";
    }

    public class FoundryPowerSustain
    {
        public string actionType { get; set; }
        public string detail { get; set; }
    }

    public class FoundryPowerAttack
    {
        public bool isAttack { get; set; } = true;
        public string def { get; set; }
        public string formula { get; set; }

        public string ability { get; set; } = "";
    }

    public class FoundryPowerHit
    {
        public bool isDamage { get; set; } = true;
        public string detail { get; set; }
        public string formula { get; set; }
        public string critFormula { get; set; }
    }

    public class FoundryPowerMiss
    {
        public string detail { get; set; }
        public string formula { get; set; } = "";
    }

    public class FoundryPowerEffect
    {
        public string detail { get; set; }
    }
}