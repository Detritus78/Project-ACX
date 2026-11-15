using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Managers;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    /// <summary>
    /// Project ACX: nether versions of every loot weapon that comes in elements (acx/Content/nether-weapons).
    /// Any weapon of such a family, plain or elemental, turns into its nether version with chance acx_nether_weapon_chance,
    /// at loot tier acx_nether_weapon_min_tier and up. Generated from the weapon loot tables by acx/scripts/nether-weapons/.
    /// </summary>
    public static class AcxNetherWeaponWcids
    {
        private static readonly Dictionary<WeenieClassName, uint> netherVersions = new Dictionary<WeenieClassName, uint>()
        {
            // Nether Assagai
            { WeenieClassName.ace41037_acidassagai, 900100 },
            { WeenieClassName.ace41036_assagai, 900100 },
            { WeenieClassName.ace41039_flamingassagai, 900100 },
            { WeenieClassName.ace41040_frostassagai, 900100 },
            { WeenieClassName.ace41038_lightningassagai, 900100 },
            // Nether Atlatl
            { WeenieClassName.atlatlacid, 900101 },
            { WeenieClassName.atlatl, 900101 },
            { WeenieClassName.atlatlblunt, 900101 },
            { WeenieClassName.atlatlelectric, 900101 },
            { WeenieClassName.atlatlfire, 900101 },
            { WeenieClassName.atlatlfrost, 900101 },
            { WeenieClassName.atlatlpiercing, 900101 },
            { WeenieClassName.atlatlslashing, 900101 },
            // Nether Bastone
            { WeenieClassName.staffmeleebastoneacid, 900102 },
            { WeenieClassName.staffmeleebastone, 900102 },
            { WeenieClassName.staffmeleebastonefire, 900102 },
            { WeenieClassName.staffmeleebastonefrost, 900102 },
            { WeenieClassName.staffmeleebastoneelectric, 900102 },
            // Nether Battle Axe
            { WeenieClassName.axebattleacid, 900103 },
            { WeenieClassName.axebattle, 900103 },
            { WeenieClassName.axebattlefire, 900103 },
            { WeenieClassName.axebattlefrost, 900103 },
            { WeenieClassName.axebattleelectric, 900103 },
            // Nether Board with Nail
            { WeenieClassName.ace31775_acidboardwithnail, 900104 },
            { WeenieClassName.ace31774_boardwithnail, 900104 },
            { WeenieClassName.ace31776_electricboardwithnail, 900104 },
            { WeenieClassName.ace31777_fireboardwithnail, 900104 },
            { WeenieClassName.ace31773_frostboardwithnail, 900104 },
            // Nether Bow
            { WeenieClassName.bowacid, 900105 },
            { WeenieClassName.bowblunt, 900105 },
            { WeenieClassName.bowelectric, 900105 },
            { WeenieClassName.bowfire, 900105 },
            { WeenieClassName.bowfrost, 900105 },
            { WeenieClassName.bowpiercing, 900105 },
            { WeenieClassName.bowslashing, 900105 },
            // Nether Broad Sword
            { WeenieClassName.swordbroadacid, 900106 },
            { WeenieClassName.swordbroad, 900106 },
            { WeenieClassName.swordbroadfire, 900106 },
            { WeenieClassName.swordbroadfrost, 900106 },
            { WeenieClassName.swordbroadelectric, 900106 },
            // Nether Budiaq
            { WeenieClassName.budiaqacid, 900107 },
            { WeenieClassName.budiaq, 900107 },
            { WeenieClassName.budiaqfire, 900107 },
            { WeenieClassName.budiaqfrost, 900107 },
            { WeenieClassName.budiaqelectric, 900107 },
            // Nether Cestus
            { WeenieClassName.cestusacid, 900108 },
            { WeenieClassName.cestus, 900108 },
            { WeenieClassName.cestusfire, 900108 },
            { WeenieClassName.cestusfrost, 900108 },
            { WeenieClassName.cestuselectric, 900108 },
            // Nether Claw
            { WeenieClassName.ace31785_acidclaw, 900109 },
            { WeenieClassName.ace31784_claw, 900109 },
            { WeenieClassName.ace31787_flamingclaw, 900109 },
            { WeenieClassName.ace31783_frostclaw, 900109 },
            { WeenieClassName.ace31786_lightningclaw, 900109 },
            // Nether Club
            { WeenieClassName.clubacid, 900110 },
            { WeenieClassName.club, 900110 },
            { WeenieClassName.clubfire, 900110 },
            { WeenieClassName.clubfrost, 900110 },
            { WeenieClassName.clubelectric, 900110 },
            // Nether Compound Bow
            { WeenieClassName.ace31799_acidcompoundbow, 900111 },
            { WeenieClassName.ace31800_bluntcompoundbow, 900111 },
            { WeenieClassName.ace31801_electriccompoundbow, 900111 },
            { WeenieClassName.ace31802_firecompoundbow, 900111 },
            { WeenieClassName.ace31803_frostcompoundbow, 900111 },
            { WeenieClassName.ace31804_piercingcompoundbow, 900111 },
            { WeenieClassName.ace31798_slashingcompoundbow, 900111 },
            // Nether Compound Crossbow
            { WeenieClassName.ace31806_acidcompoundcrossbow, 900112 },
            { WeenieClassName.ace31807_bluntcompoundcrossbow, 900112 },
            { WeenieClassName.ace31808_electriccompoundcrossbow, 900112 },
            { WeenieClassName.ace31809_firecompoundcrossbow, 900112 },
            { WeenieClassName.ace31810_frostcompoundcrossbow, 900112 },
            { WeenieClassName.ace31811_piercingcompoundcrossbow, 900112 },
            { WeenieClassName.ace31805_slashingcompoundcrossbow, 900112 },
            // Nether Corsesca
            { WeenieClassName.ace40819_acidcorsesca, 900113 },
            { WeenieClassName.ace40818_corsesca, 900113 },
            { WeenieClassName.ace40821_flamingcorsesca, 900113 },
            { WeenieClassName.ace40822_frostcorsesca, 900113 },
            { WeenieClassName.ace40820_lightningcorsesca, 900113 },
            // Nether Crossbow
            { WeenieClassName.crossbowacid, 900114 },
            { WeenieClassName.crossbowblunt, 900114 },
            { WeenieClassName.crossbowelectric, 900114 },
            { WeenieClassName.crossbowfire, 900114 },
            { WeenieClassName.crossbowfrost, 900114 },
            { WeenieClassName.crossbowpiercing, 900114 },
            { WeenieClassName.crossbowslashing, 900114 },
            // Nether Dabus
            { WeenieClassName.dabusacid, 900115 },
            { WeenieClassName.dabus, 900115 },
            { WeenieClassName.dabusfire, 900115 },
            { WeenieClassName.dabusfrost, 900115 },
            { WeenieClassName.dabuselectric, 900115 },
            // Nether Dagger
            { WeenieClassName.daggeracid, 900116 },
            { WeenieClassName.dagger, 900116 },
            { WeenieClassName.daggerfire, 900116 },
            { WeenieClassName.daggerfrost, 900116 },
            { WeenieClassName.daggerelectric, 900116 },
            // Nether Dericost Blade
            { WeenieClassName.ace31760_aciddericostblade, 900117 },
            { WeenieClassName.ace31759_dericostblade, 900117 },
            { WeenieClassName.ace31762_flamingdericostblade, 900117 },
            { WeenieClassName.ace31758_frostdericostblade, 900117 },
            { WeenieClassName.ace31761_lightningdericostblade, 900117 },
            // Nether Dirk
            { WeenieClassName.dirkacid, 900118 },
            { WeenieClassName.dirk, 900118 },
            { WeenieClassName.dirkfire, 900118 },
            { WeenieClassName.dirkfrost, 900118 },
            { WeenieClassName.dirkelectric, 900118 },
            // Nether Dolabra
            { WeenieClassName.axedolabraacid, 900119 },
            { WeenieClassName.axedolabra, 900119 },
            { WeenieClassName.axedolabrafire, 900119 },
            { WeenieClassName.axedolabrafrost, 900119 },
            { WeenieClassName.axedolabraelectric, 900119 },
            // Nether Epee
            { WeenieClassName.ace45100_acidepee, 900120 },
            { WeenieClassName.ace45099_epee, 900120 },
            { WeenieClassName.ace45102_flamingepee, 900120 },
            { WeenieClassName.ace45103_frostepee, 900120 },
            { WeenieClassName.ace45101_lightningepee, 900120 },
            // Nether Flamberge
            { WeenieClassName.swordflambergeacid, 900121 },
            { WeenieClassName.swordflamberge, 900121 },
            { WeenieClassName.swordflambergefire, 900121 },
            { WeenieClassName.swordflambergefrost, 900121 },
            { WeenieClassName.swordflambergeelectric, 900121 },
            // Nether Flanged Mace
            { WeenieClassName.maceflangedacid, 900122 },
            { WeenieClassName.maceflangedfire, 900122 },
            { WeenieClassName.maceflanged, 900122 },
            { WeenieClassName.maceflangedfrost, 900122 },
            { WeenieClassName.maceflangedelectric, 900122 },
            // Nether Great Star Mace
            { WeenieClassName.ace41058_acidgreatstarmace, 900123 },
            { WeenieClassName.ace41060_flaminggreatstarmace, 900123 },
            { WeenieClassName.ace41061_frostgreatstarmace, 900123 },
            { WeenieClassName.ace41057_greatstarmace, 900123 },
            { WeenieClassName.ace41059_lightninggreatstarmace, 900123 },
            // Nether Greataxe
            { WeenieClassName.ace41053_acidgreataxe, 900124 },
            { WeenieClassName.ace41055_flaminggreataxe, 900124 },
            { WeenieClassName.ace41056_frostgreataxe, 900124 },
            { WeenieClassName.ace41052_greataxe, 900124 },
            { WeenieClassName.ace41054_lightninggreataxe, 900124 },
            // Nether Hand Axe
            { WeenieClassName.axehandacid, 900125 },
            { WeenieClassName.axehandfire, 900125 },
            { WeenieClassName.axehandfrost, 900125 },
            { WeenieClassName.axehand, 900125 },
            { WeenieClassName.axehandelectric, 900125 },
            // Nether Hand Wraps
            { WeenieClassName.ace45119_acidhandwraps, 900126 },
            { WeenieClassName.ace45121_flaminghandwraps, 900126 },
            { WeenieClassName.ace45122_frosthandwraps, 900126 },
            { WeenieClassName.ace45118_handwraps, 900126 },
            { WeenieClassName.ace45120_lightninghandwraps, 900126 },
            // Nether Hatchet
            { WeenieClassName.axehatchetacid, 900127 },
            { WeenieClassName.axehatchetfire, 900127 },
            { WeenieClassName.axehatchetfrost, 900127 },
            { WeenieClassName.axehatchet, 900127 },
            { WeenieClassName.axehatchetelectric, 900127 },
            // Nether Jambiya
            { WeenieClassName.jambiyaacid, 900128 },
            { WeenieClassName.jambiyafire, 900128 },
            { WeenieClassName.jambiyafrost, 900128 },
            { WeenieClassName.jambiya, 900128 },
            { WeenieClassName.jambiyaelectric, 900128 },
            // Nether Jitte
            { WeenieClassName.jitteacid, 900129 },
            { WeenieClassName.jittefire, 900129 },
            { WeenieClassName.jittefrost, 900129 },
            { WeenieClassName.jitte, 900129 },
            { WeenieClassName.jitteelectric, 900129 },
            // Nether Jo
            { WeenieClassName.joacidnew, 900130 },
            { WeenieClassName.jofirenew, 900130 },
            { WeenieClassName.jofrostnew, 900130 },
            { WeenieClassName.jonew, 900130 },
            { WeenieClassName.joelectricnew, 900130 },
            // Nether Kaskara
            { WeenieClassName.kaskaraacid, 900131 },
            { WeenieClassName.kaskarafire, 900131 },
            { WeenieClassName.kaskarafrost, 900131 },
            { WeenieClassName.kaskara, 900131 },
            { WeenieClassName.kaskaraelectric, 900131 },
            // Nether Kasrullah
            { WeenieClassName.kasrullahacid, 900132 },
            { WeenieClassName.kasrullahfire, 900132 },
            { WeenieClassName.kasrullahfrost, 900132 },
            { WeenieClassName.kasrullah, 900132 },
            { WeenieClassName.kasrullahelectric, 900132 },
            // Nether Katar
            { WeenieClassName.kataracid, 900133 },
            { WeenieClassName.katarfire, 900133 },
            { WeenieClassName.katarfrost, 900133 },
            { WeenieClassName.katar, 900133 },
            { WeenieClassName.katarelectric, 900133 },
            // Nether Ken
            { WeenieClassName.kenacid, 900134 },
            { WeenieClassName.kenfire, 900134 },
            { WeenieClassName.kenfrost, 900134 },
            { WeenieClassName.ken, 900134 },
            { WeenieClassName.kenelectric, 900134 },
            // Nether Khanda-handled Mace
            { WeenieClassName.ace41063_acidkhandahandledmace, 900135 },
            { WeenieClassName.ace41065_flamingkhandahandledmace, 900135 },
            { WeenieClassName.ace41066_frostkhandahandledmace, 900135 },
            { WeenieClassName.ace41062_khandahandledmace, 900135 },
            { WeenieClassName.ace41064_lightningkhandahandledmace, 900135 },
            // Nether Khanjar
            { WeenieClassName.khanjaracid, 900136 },
            { WeenieClassName.khanjarfire, 900136 },
            { WeenieClassName.khanjarfrost, 900136 },
            { WeenieClassName.khanjar, 900136 },
            { WeenieClassName.khanjarelectric, 900136 },
            // Nether Knife
            { WeenieClassName.knifeacid, 900137 },
            { WeenieClassName.knifefire, 900137 },
            { WeenieClassName.knifefrost, 900137 },
            { WeenieClassName.knife, 900137 },
            { WeenieClassName.knifeelectric, 900137 },
            // Nether Knuckles
            { WeenieClassName.knucklesacid, 900138 },
            { WeenieClassName.knucklesfire, 900138 },
            { WeenieClassName.knucklesfrost, 900138 },
            { WeenieClassName.knuckles, 900138 },
            { WeenieClassName.knuckleselectric, 900138 },
            // Nether Lancet
            { WeenieClassName.ace31795_acidlancet, 900139 },
            { WeenieClassName.ace31797_flaminglancet, 900139 },
            { WeenieClassName.ace31793_frostlancet, 900139 },
            { WeenieClassName.ace31794_lancet, 900139 },
            { WeenieClassName.ace31796_lightninglancet, 900139 },
            // Nether Long Sword
            { WeenieClassName.swordlongacid, 900140 },
            { WeenieClassName.swordlongfire, 900140 },
            { WeenieClassName.swordlongfrost, 900140 },
            { WeenieClassName.swordlongelectric, 900140 },
            { WeenieClassName.swordlong, 900140 },
            // Nether Lugian Hammer
            { WeenieClassName.ace31765_acidlugianhammer, 900141 },
            { WeenieClassName.ace31767_flaminglugianhammer, 900141 },
            { WeenieClassName.ace31763_frostlugianhammer, 900141 },
            { WeenieClassName.ace31766_lightninglugianhammer, 900141 },
            { WeenieClassName.ace31764_lugianhammer, 900141 },
            // Nether Mace
            { WeenieClassName.maceacid, 900142 },
            { WeenieClassName.macefire, 900142 },
            { WeenieClassName.macefrost, 900142 },
            { WeenieClassName.maceelectric, 900142 },
            { WeenieClassName.mace, 900142 },
            // Nether Magari Yari
            { WeenieClassName.ace41042_acidmagariyari, 900143 },
            { WeenieClassName.ace41044_flamingmagariyari, 900143 },
            { WeenieClassName.ace41045_frostmagariyari, 900143 },
            { WeenieClassName.ace41043_lightningmagariyari, 900143 },
            { WeenieClassName.ace41041_magariyari, 900143 },
            // Nether Mazule
            { WeenieClassName.macemazuleacid, 900144 },
            { WeenieClassName.macemazulefire, 900144 },
            { WeenieClassName.macemazulefrost, 900144 },
            { WeenieClassName.macemazuleelectric, 900144 },
            { WeenieClassName.macemazule, 900144 },
            // Nether Morning Star
            { WeenieClassName.morningstaracid, 900145 },
            { WeenieClassName.morningstarfire, 900145 },
            { WeenieClassName.morningstarfrost, 900145 },
            { WeenieClassName.morningstarelectric, 900145 },
            { WeenieClassName.morningstar, 900145 },
            // Nether Nabut
            { WeenieClassName.nabutacidnew, 900146 },
            { WeenieClassName.nabutfirenew, 900146 },
            { WeenieClassName.nabutfrostnew, 900146 },
            { WeenieClassName.nabutelectricnew, 900146 },
            { WeenieClassName.nabutnew, 900146 },
            // Nether Naginata
            { WeenieClassName.swordstaffacid, 900147 },
            { WeenieClassName.swordstaffelectric, 900147 },
            { WeenieClassName.swordstafffire, 900147 },
            { WeenieClassName.swordstafffrost, 900147 },
            { WeenieClassName.swordstaff, 900147 },
            // Nether Nekode
            { WeenieClassName.nekodeacid, 900148 },
            { WeenieClassName.nekodefire, 900148 },
            { WeenieClassName.nekodefrost, 900148 },
            { WeenieClassName.nekodeelectric, 900148 },
            { WeenieClassName.nekode, 900148 },
            // Nether Nodachi
            { WeenieClassName.ace40761_acidnodachi, 900149 },
            { WeenieClassName.ace40763_flamingnodachi, 900149 },
            { WeenieClassName.ace40764_frostnodachi, 900149 },
            { WeenieClassName.ace40762_lightningnodachi, 900149 },
            { WeenieClassName.ace40760_nodachi, 900149 },
            // Nether Ono
            { WeenieClassName.onoacid, 900150 },
            { WeenieClassName.onofire, 900150 },
            { WeenieClassName.onofrost, 900150 },
            { WeenieClassName.onoelectric, 900150 },
            { WeenieClassName.ono, 900150 },
            // Nether Partizan
            { WeenieClassName.spearpartizanacid, 900151 },
            { WeenieClassName.spearpartizanfire, 900151 },
            { WeenieClassName.spearpartizanfrost, 900151 },
            { WeenieClassName.spearpartizanelectric, 900151 },
            { WeenieClassName.spearpartizan, 900151 },
            // Nether Pike
            { WeenieClassName.ace41047_acidpike, 900152 },
            { WeenieClassName.ace41049_flamingpike, 900152 },
            { WeenieClassName.ace41050_frostpike, 900152 },
            { WeenieClassName.ace41048_lightningpike, 900152 },
            { WeenieClassName.ace41046_pike, 900152 },
            // Nether Poniard
            { WeenieClassName.daggerponiardacid, 900153 },
            { WeenieClassName.daggerponiardfire, 900153 },
            { WeenieClassName.daggerponiardfrost, 900153 },
            { WeenieClassName.daggerponiardelectric, 900153 },
            { WeenieClassName.daggerponiard, 900153 },
            // Nether Quadrelle
            { WeenieClassName.ace40624_acidquadrelle, 900154 },
            { WeenieClassName.ace40626_flamingquadrelle, 900154 },
            { WeenieClassName.ace40627_frostquadrelle, 900154 },
            { WeenieClassName.ace40625_lightningquadrelle, 900154 },
            { WeenieClassName.ace40623_quadrelle, 900154 },
            // Nether Quarter Staff
            { WeenieClassName.quarterstaffacidnew, 900155 },
            { WeenieClassName.quarterstaffflamenew, 900155 },
            { WeenieClassName.quarterstafffrostnew, 900155 },
            { WeenieClassName.quarterstaffelectricnew, 900155 },
            { WeenieClassName.quarterstaffnew, 900155 },
            // Nether Rapier
            { WeenieClassName.ace45104_acidrapier, 900156 },
            { WeenieClassName.ace45106_flamingrapier, 900156 },
            { WeenieClassName.ace45107_frostrapier, 900156 },
            { WeenieClassName.ace45105_lightningrapier, 900156 },
            { WeenieClassName.swordrapier, 900156 },
            // Nether Sabra
            { WeenieClassName.swordsabraacid, 900157 },
            { WeenieClassName.swordsabrafire, 900157 },
            { WeenieClassName.swordsabrafrost, 900157 },
            { WeenieClassName.swordsabraelectric, 900157 },
            { WeenieClassName.swordsabra, 900157 },
            // Nether Schlager
            { WeenieClassName.ace45109_acidschlager, 900158 },
            { WeenieClassName.ace45111_flamingschlager, 900158 },
            { WeenieClassName.ace45112_frostschlager, 900158 },
            { WeenieClassName.ace45110_lightningschlager, 900158 },
            { WeenieClassName.ace45108_schlager, 900158 },
            // Nether Scimitar
            { WeenieClassName.scimitaracid, 900159 },
            { WeenieClassName.scimitarfire, 900159 },
            { WeenieClassName.scimitarfrost, 900159 },
            { WeenieClassName.scimitarelectric, 900159 },
            { WeenieClassName.scimitar, 900159 },
            // Nether Shamshir
            { WeenieClassName.shamshiracid, 900160 },
            { WeenieClassName.shamshirfire, 900160 },
            { WeenieClassName.shamshirfrost, 900160 },
            { WeenieClassName.shamshirelectric, 900160 },
            { WeenieClassName.shamshir, 900160 },
            // Nether Shashqa
            { WeenieClassName.ace41068_acidshashqa, 900161 },
            { WeenieClassName.ace41070_flamingshashqa, 900161 },
            { WeenieClassName.ace41071_frostshashqa, 900161 },
            { WeenieClassName.ace41069_lightningshashqa, 900161 },
            { WeenieClassName.ace41067_shashqa, 900161 },
            // Nether Short Sword
            { WeenieClassName.swordshortacid, 900162 },
            { WeenieClassName.swordshortfire, 900162 },
            { WeenieClassName.swordshortfrost, 900162 },
            { WeenieClassName.swordshortelectric, 900162 },
            { WeenieClassName.swordshort, 900162 },
            // Nether Shou-ono
            { WeenieClassName.shouonoacid, 900163 },
            { WeenieClassName.shouonofire, 900163 },
            { WeenieClassName.shouonofrost, 900163 },
            { WeenieClassName.shouonoelectric, 900163 },
            { WeenieClassName.shouono, 900163 },
            // Nether Silifi
            { WeenieClassName.silifiacid, 900164 },
            { WeenieClassName.silififire, 900164 },
            { WeenieClassName.silififrost, 900164 },
            { WeenieClassName.silifielectric, 900164 },
            { WeenieClassName.silifi, 900164 },
            // Nether Simi
            { WeenieClassName.simiacid, 900165 },
            { WeenieClassName.simifire, 900165 },
            { WeenieClassName.simifrost, 900165 },
            { WeenieClassName.simielectric, 900165 },
            { WeenieClassName.simi, 900165 },
            // Nether Slingshot
            { WeenieClassName.ace31813_acidslingshot, 900166 },
            { WeenieClassName.ace31814_bluntslingshot, 900166 },
            { WeenieClassName.ace31815_electricslingshot, 900166 },
            { WeenieClassName.ace31816_fireslingshot, 900166 },
            { WeenieClassName.ace31817_frostslingshot, 900166 },
            { WeenieClassName.ace31818_piercingslingshot, 900166 },
            { WeenieClassName.ace31812_slashingslingshot, 900166 },
            // Nether Spada
            { WeenieClassName.swordspadafrost, 900167 },
            { WeenieClassName.swordspadafire, 900167 },
            { WeenieClassName.swordspadaelectric, 900167 },
            { WeenieClassName.swordspadaacid, 900167 },
            { WeenieClassName.swordspada, 900167 },
            // Nether Spadone
            { WeenieClassName.ace40619_acidspadone, 900168 },
            { WeenieClassName.ace40621_flamingspadone, 900168 },
            { WeenieClassName.ace40622_frostspadone, 900168 },
            { WeenieClassName.ace40620_lightningspadone, 900168 },
            { WeenieClassName.ace40618_spadone, 900168 },
            // Nether Spear
            { WeenieClassName.spearacid, 900169 },
            { WeenieClassName.spearflame, 900169 },
            { WeenieClassName.spearfrost, 900169 },
            { WeenieClassName.spearelectric, 900169 },
            { WeenieClassName.spear, 900169 },
            // Nether Spiked Club
            { WeenieClassName.clubspikedacid, 900170 },
            { WeenieClassName.clubspikedelectric, 900170 },
            { WeenieClassName.clubspikedfire, 900170 },
            { WeenieClassName.clubspikedfrost, 900170 },
            { WeenieClassName.clubspiked, 900170 },
            // Nether Spine Glaive
            { WeenieClassName.ace31780_acidspineglaive, 900171 },
            { WeenieClassName.ace31781_electricspineglaive, 900171 },
            { WeenieClassName.ace31782_firespineglaive, 900171 },
            { WeenieClassName.ace31778_frostspineglaive, 900171 },
            { WeenieClassName.ace31779_spineglaive, 900171 },
            // Nether Stick
            { WeenieClassName.ace31789_acidstick, 900172 },
            { WeenieClassName.ace31791_flamingstick, 900172 },
            { WeenieClassName.ace31792_froststick, 900172 },
            { WeenieClassName.ace31790_lightningstick, 900172 },
            { WeenieClassName.ace31788_stick, 900172 },
            // Nether Stiletto
            { WeenieClassName.daggerstilettoacid, 900173 },
            { WeenieClassName.daggerstilettofire, 900173 },
            { WeenieClassName.daggerstilettofrost, 900173 },
            { WeenieClassName.daggerstilettoelectric, 900173 },
            { WeenieClassName.daggerstiletto, 900173 },
            // Nether Tachi
            { WeenieClassName.tachiacid, 900174 },
            { WeenieClassName.tachifire, 900174 },
            { WeenieClassName.tachifrost, 900174 },
            { WeenieClassName.tachielectric, 900174 },
            { WeenieClassName.tachi, 900174 },
            // Nether Takuba
            { WeenieClassName.takubaacid, 900175 },
            { WeenieClassName.takubafire, 900175 },
            { WeenieClassName.takubafrost, 900175 },
            { WeenieClassName.takubaelectric, 900175 },
            { WeenieClassName.takuba, 900175 },
            // Nether Tetsubo
            { WeenieClassName.ace40636_acidtetsubo, 900176 },
            { WeenieClassName.ace40638_flamingtetsubo, 900176 },
            { WeenieClassName.ace40639_frosttetsubo, 900176 },
            { WeenieClassName.ace40637_lightningtetsubo, 900176 },
            { WeenieClassName.ace40635_tetsubo, 900176 },
            // Nether Tofun
            { WeenieClassName.tofunacid, 900177 },
            { WeenieClassName.tofunfire, 900177 },
            { WeenieClassName.tofunfrost, 900177 },
            { WeenieClassName.tofunelectric, 900177 },
            { WeenieClassName.tofun, 900177 },
            // Nether Trident
            { WeenieClassName.tridentacid, 900178 },
            { WeenieClassName.tridentelectric, 900178 },
            { WeenieClassName.tridentfire, 900178 },
            { WeenieClassName.tridentfrost, 900178 },
            { WeenieClassName.trident, 900178 },
            // Nether Tungi
            { WeenieClassName.tungiacid, 900179 },
            { WeenieClassName.tungifire, 900179 },
            { WeenieClassName.tungifrost, 900179 },
            { WeenieClassName.tungielectric, 900179 },
            { WeenieClassName.tungi, 900179 },
            // Nether War Axe
            { WeenieClassName.ace31770_acidwaraxe, 900180 },
            { WeenieClassName.ace31772_flamingwaraxe, 900180 },
            { WeenieClassName.ace31768_frostwaraxe, 900180 },
            { WeenieClassName.ace31771_lightningwaraxe, 900180 },
            { WeenieClassName.ace31769_waraxe, 900180 },
            // Nether War Hammer
            { WeenieClassName.warhammeracid, 900181 },
            { WeenieClassName.warhammerfire, 900181 },
            { WeenieClassName.warhammerfrost, 900181 },
            { WeenieClassName.warhammerelectric, 900181 },
            { WeenieClassName.warhammer, 900181 },
            // Nether Yaoji
            { WeenieClassName.yaojiacid, 900182 },
            { WeenieClassName.yaojifire, 900182 },
            { WeenieClassName.yaojifrost, 900182 },
            { WeenieClassName.yaojielectric, 900182 },
            { WeenieClassName.yaoji, 900182 },
            // Nether Yari
            { WeenieClassName.yariacid, 900183 },
            { WeenieClassName.yarifire, 900183 },
            { WeenieClassName.yarifrost, 900183 },
            { WeenieClassName.yarielectric, 900183 },
            { WeenieClassName.yari, 900183 },
        };

        public static WeenieClassName TryRollNether(TreasureDeath treasureDeath, WeenieClassName wcid)
        {
            uint netherWcid = 0;
            if (treasureDeath.Tier < PropertyManager.GetLong("acx_nether_weapon_min_tier").Item
                || !(netherVersions.TryGetValue(wcid, out netherWcid) || AcxThrownWcids.TryGetNether(wcid, out netherWcid)))   // Eternal thrown weapons: their own table
                return wcid;

            if (ThreadSafeRandom.Next(0.0f, 1.0f) >= PropertyManager.GetDouble("acx_nether_weapon_chance").Item)
                return wcid;

            return (WeenieClassName)netherWcid;
        }
    }
}
