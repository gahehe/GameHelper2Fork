// <copyright file="SekhemaAfflictionText.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    internal readonly record struct AfflictionText(string Description, bool Major);

    internal static class SekhemaAfflictionText
    {
        public static bool TryGet(string name, out AfflictionText text)
        {
            text = name switch
            {
                "Black Smoke" => new("You can see one fewer room ahead on the Trial Map", false),
                "Blunt Sword" => new("You and your Minions deal 40% less Damage", false),
                "Branded Balbalakh" => new("Cannot restore Honour", true),
                "Chains of Binding" => new("Monsters inflict Binding Chains on Hit", false),
                "Chiselled Stone" => new("Monsters Petrify on Hit", true),
                "Corrosive Concoction" => new("You have no Armour, Evasion and Energy Shield. Weight is computed from your defences", true),
                "Costly Aid" => new("Gain a random Minor Affliction when you venerate a Maraketh Shrine", false),
                "Dark Pit" => new("Traps deal 100% increased Damage", false),
                "Deadly Snare" => new("Traps deal Triple Damage", true),
                "Death Toll" => new("Monsters no longer drop Sacred Water. Take Physical Damage after completing rooms", true),
                "Deceptive Mirror" => new("You are not always taken to the room you select", false),
                "Dishonoured Tattoo" => new("100% increased Damage Taken while on Low Life", false),
                "Exhausted Wells" => new("Chests no longer grant Sacred Water", false),
                "Fiendish Wings" => new("Monsters are faster and cannot be slowed below base", false),
                "Forgotten Traditions" => new("50% reduced Effect of your Non-Unique Relics", false),
                "Gate Toll" => new("Lose 30 Sacred Water on room completion", false),
                "Ghastly Scythe" => new("Losing Honour ends the Trial", true),
                "Glass Shard" => new("The next Boon you gain is converted into a random Minor Affliction", true),
                "Golden Smoke" => new("Rewards are unknown on the Trial Map", false),
                "Haemorrhage" => new("You cannot restore Honour until the next Boss", false),
                "Honed Claws" => new("Monsters deal 30% more Damage", false),
                "Hungry Fangs" => new("Monsters remove 5% of your Life, Mana and Energy Shield on Hit", false),
                "Iron Manacles" => new("You have no Evasion. Weight is computed from your defences", false),
                "Leaking Waterskin" => new("Lose 20 Sacred Water when you take Damage from an Enemy Hit", false),
                "Low Rivers" => new("50% less Sacred Water found", false),
                "Myriad Aspersions" => new("When you gain an Affliction, gain an additional random Minor Affliction", false),
                "Orb of Negation" => new("Non-Unique Relics have no Effect", true),
                "Orbala's Leathers" => new("50% less Maximum Honour", true),
                "Purple Smoke" => new("Afflictions are unknown on the Trial Map", false),
                "Rapid Quicksand" => new("Traps are faster", false),
                "Red Smoke" => new("Room types are unknown on the Trial Map", false),
                "Rusted Mallet" => new("Monsters always Knockback", false),
                "Season of Famine" => new("The Merchant offers 50% fewer choices", false),
                "Sharpened Arrowhead" => new("You have no Armour. Weight is computed from your defences", false),
                "Shattered Shield" => new("You have 50% less Energy Shield. Weight is computed from your defences", false),
                "Spiked Exit" => new("Take Physical Damage on Room Completion", false),
                "Spiked Shell" => new("Monsters have 50% increased Maximum Life", false),
                "Suspected Sympathiser" => new("50% reduced Honour restored", false),
                "Tattered Blindfold" => new("90% reduced Light Radius. Minimap is hidden", false),
                "Trade Tariff" => new("50% increased Merchant prices", false),
                "Tradition's Demand" => new("The Merchant only offers one choice", false),
                "Unassuming Brick" => new("You cannot gain any more Boons", true),
                "Unquenched Thirst" => new("You cannot gain Sacred Water", true),
                "Untouchable" => new("You are Cursed with Enfeeble", false),
                "Veiled Sight" => new("Rooms are unknown on the Trial Map", true),
                "Weakened Flesh" => new("25% less Maximum Honour", false),
                "Winter Drought" => new("Lose all Sacred Water on floor completion", false),
                "Worn Sandals" => new("30% less Movement Speed if you've been Hit Recently. Ignored with Queen of the Forest", false),
                _ => default,
            };
            return text.Description is { Length: > 0 };
        }
    }
}
