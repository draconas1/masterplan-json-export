# Creature Exporter for MasterPlan

An AddIn for Masterplan for those still using it to DM D&D 4th Edition that will export all the creatures in an encounter into json format to allow them to be easily imported into Roll20 (or the other VTT of your choice)

Written because I am running a 4th edition game and want to do my plannning in masterplan but get my monsters easily to roll20.

Currently only does creatures, I may add trap functionality to it later if I start throwing a lot of traps at my players, for now they are few enough that i do them by hand.  

As Masterplan is sadly gone from the internet, along with all its offical documentation, this project owes a huge amount to the blog:  http://paigew.wordpress.com/2010/11/01/creating-a-masterplan-add-on-part-1/ which is the last remaining documentation on how to write an AddIn and even more to the coder guillaumejay.  Guillaumejay replied in the comments to that blog post with a link to the sourcecode for the AddIn they wrote using it.  

This code was based off a copy of Guillaumejay's Compendium Import that they put here: https://github.com/guillaumejay/CompendiumImport which was a huge kickstarter in project setup and using the API, without which it would have taken me forever to get started.  So please - always post thank you comments to blogs with examples of how you used them!  1. it makes blog writers happy and 2, you never know who will find your code years later as just the thing they were after!

# Installation

1. Goto the latest release: https://github.com/draconas1/masterplan-json-export/releases/
2. Download the zip file
3. Unzip to Masterplan\Addins

Creature Exporter will now show up under Tools -> Addins when you next start masterplan.

# Usage Instructions
# Exporting
The exporter has 2 modes: Export entire project and export selected encounter.  Both will cause a popup window to appear with 2 text windows, one of which contains a huge bloc of json and the other is an note display about problems.

The problem display indicates things that the parser was unable to resolve so you can tweak the json manually.  The most common of these is due to an issue how masterplan derives the damage block of a power - searching for the word "damage" and so putting a bunch of text in the damage section of a non damaging power than nevertheless mentions the word "damage" in its description.  Read the message and if it looks like the sort of power where you are not expecting damage then everything will be fine. 

# Importing
I am including my roll20 scripts for importing the creatures into Roll20.  For this you will need a roll20 Pro subscription: you require API access.   You will also want your game using the roll20 Dungeons and Dragons 4E Character sheet (there is only 1)

## Scripts
There are 4 separate scripts at the time of writing: Common, Import, Token-Ops and Character-Ops.  The first 2 are required for importing, Token-Ops for assigning tokens, Character-Ops gives some useful abilities for working with the character sheet.  Token-Ops only requires Masterplan-Common, Character-Ops requires Token-Ops to work.

Install them all into roll20.  you can find them here: https://github.com/draconas1/masterplan-json-export/tree/main/CreatureExport/Roll20Api

**Note**

Most of the scripts do nothing unless poked, however Token-Ops includes an token changed listener that manages temporary hitpoints - if a tokens HP bar is reduced and it has tempHP in the tempHP bar, then the tempHP are expended instead and the HP value adjusted.  If you don't want this functionality, remove the line `on("change:token", TokenOps.soakDamageOnTempHP);` at the bottom of Token-Ops.  

## Use

### Importing 
With the scripts installed, you need to create a handout called exactly: **Book About Badgers**  In the GM notes of this handout, select "code" as the style and paste the json from the exporter.  It is important that you not put any other styling on, the importer will cope with no styles and "code" style, but will be unable to read the file if there are others.  For large proejcts I find there is a lag here while roll20 tries to sort out what you have pasted, this lag is less when using the "code" style, however if your project is massive you may have to export-import individual encounters.

The importer can then be run using **!import-masterplan** You will get some output in the chat about what is happening.

The importer will run and generate new creatures for you, it will not generate a creature if one exists with an identical name.

#### Populated Fields:
* Defences
* Attributes
* Skills
* Initiative
* Class abilities is populated the monsters non-attack abilities, auras and tactics
* Powers gets the monsters attack powers
* The abilities section gets macro links to all of the monsters powers that are set up to be token-actions
* A number of other hidden fields used by the scripts below

Note that attack powers do not use the Inventory-Weapons or any of the easily inputable power fields, as monster powers are defined at specific to-hit and damages it was easier just to write the entire equation into the power macro.  Also ment using fewer calculated fields which reduces some of the complexity burden

### Assigning
With one or more tokens selected you can use the command from Token-Ops: **!assign-token CHARACTER-NAME** where **CHARACTER-NAME** is the exact name of a character handout.  

This will assign the token to that character and will set the 3 bars to be:
* *Red*: Hitpoints (with max)
* *Green*: Armour Class
* *Blue*: Temporary Hitpoints  

None of these will be tied to the character sheet, they are copies, not links.  

If the monster has auras it will also assign them and set them player-visible.  (These use attributes not shown in the character sheet)

It will not set the token as the default for that character, you can either do that manually, or I use the excellent **TokenMod** script which is available as a roll20 1 click-install.

Done, your monster tokens are now assigned to character sheets, below are some other useful script functions and macros I have set up.  

### Using

#### !apply-markers
This is a utility script I use for when I have multiple monster of the same type.  Select them all and enter **!apply-markers** in the chat and it will assign them one of the coloured status marker dots.  That way my players can refer to "I attack the pink goblin" when they have multiple outs.  If you have selected more than 7 creatures it will move onto numbered colours.

#### !know
Requires a token selected, and will message you with the monster knowledge difficulty and (if it can work it out) the appropriate knowledge power, if it can't determine the power from the creature type then it will tell you the creature type and you can make the call.  If you want to enhance the types its about line 38 in Creature-Opts, the arrays in the const knowledges object.

#### !heal
One for the players, spends a healing surge and increases hits by that amount.  **!heal [bonus]** where bonus is an optional bonus to add to the surge value (e.g. when a cleric provides a bonus with healing word), no need for a +, just put the bonus number.

#### !mp-debug-on !mp-debug-off
Turns debug output to chat and script log on or off.  

### Macros
Here are some Macros I wrote that operate with the imported monsters:
     
#### Monster Knowledge - Moderate DC:
I have this as a token action to output to the chat if the players succeed a moderate knowledge DC for the monster.

`&{template:default} {{name=@{selected|token_name}}} {{Role=@{selected|class}}}{{type=@{selected|race}}}`

#### Monster Knowledge - Hard DC:
As above, this gives the information for success at the hard DC.  **power-knowledge** is built on import and contains the powers and abilities.  I wanted to take this from the ability list to account for your edits, but it was too much of a headache.  I may one day turn this into an api script that can do that.

`&{template:default} {{name=@{selected|token_name}}} {{Role=@{selected|class}}}{{type=@{selected|race}}}{{resist/Vul=@{selected|resistances}}}@{selected|power-knowledge}`

#### Current Token - Vital Stats
Gives me a quick breakdown of HP/Max (temp HP) and defences.

`/w gm &{template:default} {{name=@{selected|token_name} Stats}} {{HP=@{selected|bar3} / @{selected|bar3|max} (@{selected|bar2}) }}{{AC=[[@{selected|ac}]]}} {{Fort=[[@{selected|fort}]]}} {{Ref=[[@{selected|ref}]]}} {{Will=[[@{selected|will}]]}}`

#### Monster saving throw
I store a save bonus for elite and solo monsters.

`[[1d20cs>[[10 - (@{selected|savebonus}) ]]cf<[[ 9 - (@{selected|savebonus}) ]] + (@{selected|savebonus})]]`

#### Heal with inputted bonus
`!heal ?{Bonus|0}`
 
### Automatic Temp HP, Bloodied and Dead
So I have this set up for my game, by assigning temp HP to the blue bar and HP to the red one, temp HP are deducted first whenever the creature takes damage.  (You may need to give it a second to work, as it needs to detect you reducing the hitpoints then perform the calculations then reset them).  Further if the token is bloodied it is tinted red, and if killed gets a big old cross.  You can disable this happening automatically by removing the line `on("change:token", TokenOps.soakDamageOnTempHP);` at the bottom of Token-Ops.js


# Your script assistant
![Fluffiest Badger and his Book](fluffiest-badger-badger-book.jpg?raw=true "Fluffiest Badger and his Book")
