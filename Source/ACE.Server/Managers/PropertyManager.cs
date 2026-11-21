using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;

using log4net;

using ACE.Database;

namespace ACE.Server.Managers
{
    public static class PropertyManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // caching internally to the server
        private static readonly ConcurrentDictionary<string, ConfigurationEntry<bool>> CachedBooleanSettings = new ConcurrentDictionary<string, ConfigurationEntry<bool>>();
        private static readonly ConcurrentDictionary<string, ConfigurationEntry<long>> CachedLongSettings = new ConcurrentDictionary<string, ConfigurationEntry<long>>();
        private static readonly ConcurrentDictionary<string, ConfigurationEntry<double>> CachedDoubleSettings = new ConcurrentDictionary<string, ConfigurationEntry<double>>();
        private static readonly ConcurrentDictionary<string, ConfigurationEntry<string>> CachedStringSettings = new ConcurrentDictionary<string, ConfigurationEntry<string>>();

        private static Timer _workerThread;

        /// <summary>
        /// Initializes the PropertyManager.
        /// Run this only once per server instance.
        /// </summary>
        /// <param name="loadDefaultValues">Should we use the DefaultPropertyManager to load the default properties for keys?</param>
        public static void Initialize(bool loadDefaultValues = true)
        {
            if (loadDefaultValues)
                DefaultPropertyManager.LoadDefaultProperties();

            LoadPropertiesFromDB();

            if (Program.IsRunningInContainer && !GetString("content_folder").Equals("/ace/Content"))
                ModifyString("content_folder", "/ace/Content");

            _workerThread = new Timer(300000);
            _workerThread.Elapsed += DoWork;
            _workerThread.AutoReset = true;
            _workerThread.Start();
        }


        /// <summary>
        /// Loads the variables from the database directly into the cache.
        /// </summary>
        private static void LoadPropertiesFromDB()
        {
            foreach (var i in DatabaseManager.ShardConfig.GetAllBools())
                CachedBooleanSettings[i.Key] = new ConfigurationEntry<bool>(false, i.Value, i.Description);

            foreach (var i in DatabaseManager.ShardConfig.GetAllLongs())
                CachedLongSettings[i.Key] = new ConfigurationEntry<long>(false, i.Value, i.Description);

            foreach (var i in DatabaseManager.ShardConfig.GetAllDoubles())
                CachedDoubleSettings[i.Key] = new ConfigurationEntry<double>(false, i.Value, i.Description);

            foreach (var i in DatabaseManager.ShardConfig.GetAllStrings())
                CachedStringSettings[i.Key] = new ConfigurationEntry<string>(false, i.Value, i.Description);
        }

        /// <summary>
        /// Resyncs the variables with the database manually.
        /// Disables the timer so that the elapsed event cannot run during the update operation.
        /// </summary>
        public static void ResyncVariables()
        {
            _workerThread.Stop();

            DoWork(null, null);

            _workerThread.Start();
        }

        /// <summary>
        /// Stops updating the cached store from the database.
        /// </summary>
        public static void StopUpdating()
        {
            if (_workerThread != null)
                _workerThread.Stop();
        }


        /// <summary>
        /// Retrieves a boolean property from the cache or database
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="fallback">The value to return if the property cannot be found.</param>
        /// <param name="cacheFallback">Whether or not the fallback property should be cached.</param>
        /// <returns>A boolean value representing the property</returns>
        public static Property<bool> GetBool(string key, bool fallback = false, bool cacheFallback = true)
        {
            // first, check the cache. If the key exists in the cache, grab it regardless of its modified value
            // then, check the database. if the key exists in the database, grab it and cache it
            // finally, set it to a default of false.
            if (CachedBooleanSettings.ContainsKey(key))
                return new Property<bool>(CachedBooleanSettings[key].Item, CachedBooleanSettings[key].Description);

            var dbValue = DatabaseManager.ShardConfig.GetBool(key);

            bool useFallback = dbValue?.Value == null;

            var value = dbValue?.Value ?? fallback;

            if (!useFallback || cacheFallback)
                CachedBooleanSettings[key] = new ConfigurationEntry<bool>(useFallback, value, dbValue?.Description);

            return new Property<bool>(value, dbValue?.Description);
        }

        /// <summary>
        /// Modifies a boolean value in the cache and marks it for being synced on the next cycle.
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="newVal">The value to replace the old value with</param>
        /// <returns>true if the property was modified, false if no property exists with the given key</returns>
        public static bool ModifyBool(string key, bool newVal)
        {
            if (!DefaultPropertyManager.DefaultBooleanProperties.ContainsKey(key))
                return false;

            if (CachedBooleanSettings.ContainsKey(key))
                CachedBooleanSettings[key].Modify(newVal);
            else
                CachedBooleanSettings[key] = new ConfigurationEntry<bool>(true, newVal, DefaultPropertyManager.DefaultBooleanProperties[key].Description);

            return true;
        }

        public static void ModifyBoolDescription(string key, string description)
        {
            if (CachedBooleanSettings.ContainsKey(key))
                CachedBooleanSettings[key].ModifyDescription(description);
            else
                log.Warn($"Attempted to modify {key} which did not exist in the BOOL cache.");
        }

        /// <summary>
        /// Retreives an integer property from the cache or database
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="fallback">The value to return if the property cannot be found.</param>
        /// <param name="cacheFallback">Whether or not the fallback property should be cached</param>
        /// <returns>An integer value representing the property</returns>
        public static Property<long> GetLong(string key, long fallback = 0, bool cacheFallback = true)
        {
            if (CachedLongSettings.ContainsKey(key))
                return new Property<long>(CachedLongSettings[key].Item, CachedLongSettings[key].Description);

            var dbValue = DatabaseManager.ShardConfig.GetLong(key);

            bool useFallback = dbValue?.Value == null;

            var value = dbValue?.Value ?? fallback;

            if (!useFallback || cacheFallback)
                CachedLongSettings[key] = new ConfigurationEntry<long>(useFallback, value, dbValue?.Description);

            return new Property<long>(value, dbValue?.Description);
        }

        /// <summary>
        /// Modifies an integer value in the cache and marks it for being synced on the next cycle.
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="newVal">The value to replace the old value with</param>
        /// <returns>true if the property was modified, false if no property exists with the given key</returns>
        public static bool ModifyLong(string key, long newVal)
        {
            if (!DefaultPropertyManager.DefaultLongProperties.ContainsKey(key))
                return false;

            if (CachedLongSettings.ContainsKey(key))
                CachedLongSettings[key].Modify(newVal);
            else
                CachedLongSettings[key] = new ConfigurationEntry<long>(true, newVal, DefaultPropertyManager.DefaultLongProperties[key].Description);
            return true;
        }

        public static void ModifyLongDescription(string key, string description)
        {
            if (CachedLongSettings.ContainsKey(key))
                CachedLongSettings[key].ModifyDescription(description);
            else
                log.Warn($"Attempted to modify {key} which did not exist in the LONG cache.");
        }

        /// <summary>
        /// Retrieves a float property from the cache or database
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="fallback">The value to return if the property cannot be found.</param>
        /// <param name="cacheFallback">Whether or not the fallpack property should be cached</param>
        /// <returns>A float value representing the property</returns>
        public static Property<double> GetDouble(string key, double fallback = 0.0f, bool cacheFallback = true)
        {
            if (CachedDoubleSettings.ContainsKey(key))
                return new Property<double>(CachedDoubleSettings[key].Item, CachedDoubleSettings[key].Description);

            var dbValue = DatabaseManager.ShardConfig.GetDouble(key);

            bool useFallback = dbValue?.Value == null;

            var value = dbValue?.Value ?? fallback;

            if (!useFallback || cacheFallback)
                CachedDoubleSettings[key] = new ConfigurationEntry<double>(useFallback, value, dbValue?.Description);

            return new Property<double>(value, dbValue?.Description);
        }

        /// <summary>
        /// Modifies a float value in the cache and marks it for being synced on the next cycle.
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="newVal">The value to replace the old value with</param>
        public static bool ModifyDouble(string key, double newVal, bool init = false)
        {
            if (!DefaultPropertyManager.DefaultDoubleProperties.ContainsKey(key))
                return false;
            if (CachedDoubleSettings.ContainsKey(key))
                CachedDoubleSettings[key].Modify(newVal);
            else
                CachedDoubleSettings[key] = new ConfigurationEntry<double>(true, newVal, DefaultPropertyManager.DefaultDoubleProperties[key].Description);

            if (!init)
            {
                switch (key)
                {
                    case "cantrip_drop_rate":
                        Factories.Tables.CantripChance.ApplyNumCantripsMod();
                        break;
                    case "minor_cantrip_drop_rate":
                    case "major_cantrip_drop_rate":
                    case "epic_cantrip_drop_rate":
                    case "legendary_cantrip_drop_rate":
                        Factories.Tables.CantripChance.ApplyCantripLevelsMod();
                        break;
                }
            }
            return true;
        }

        public static void ModifyDoubleDescription(string key, string description)
        {
            if (CachedDoubleSettings.ContainsKey(key))
                CachedDoubleSettings[key].ModifyDescription(description);
            else
                log.Warn($"Attempted to modify the description of {key} which did not exist in the DOUBLE cache.");
        }

        /// <summary>
        /// Retreives a string property from the cache or database
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="fallback">The value to return if the property cannot be found.</param>
        /// <param name="cacheFallback">Whether or not the fallback value will be cached.</param>
        /// <returns>A string value representing the property</returns>
        public static Property<string> GetString(string key, string fallback = "", bool cacheFallback = true)
        {
            if (CachedStringSettings.ContainsKey(key))
                return new Property<string>(CachedStringSettings[key].Item, CachedStringSettings[key].Description);

            var dbValue = DatabaseManager.ShardConfig.GetString(key);

            bool useFallback = dbValue?.Value == null;

            var value = dbValue?.Value ?? fallback;

            if (!useFallback || cacheFallback)
                CachedStringSettings[key] = new ConfigurationEntry<string>(useFallback, value, dbValue?.Description);

            return new Property<string>(value, dbValue?.Description);
        }

        /// <summary>
        /// Modifies a string value in the cache and marks it for being synced on the next cycle
        /// </summary>
        /// <param name="key">The string key for the property</param>
        /// <param name="newVal">The value to replace the old value with</param>
        /// <returns>true if the property was modified, false if no property exists with the given key</returns>
        public static bool ModifyString(string key, string newVal)
        {
            if (!DefaultPropertyManager.DefaultStringProperties.ContainsKey(key))
                return false;

            if (CachedStringSettings.ContainsKey(key))
                CachedStringSettings[key].Modify(newVal);
            else
                CachedStringSettings[key] = new ConfigurationEntry<string>(true, newVal, DefaultPropertyManager.DefaultStringProperties[key].Description);
            return true;
        }

        public static void ModifyStringDescription(string key, string description)
        {
            if (CachedStringSettings.ContainsKey(key))
                CachedStringSettings[key].ModifyDescription(description);
            else
                log.Warn($"Attempted to modify {key} which did not exist in the STRING cache.");
        }


        /// <summary>
        /// Writes all of the updated boolean values from the cache into the database.
        /// </summary>
        private static void WriteBoolToDB()
        {
            foreach (var i in CachedBooleanSettings.Where(r => r.Value.Modified))
            {
                // this probably should be upsert. This does 2 queries per modified datapoint.
                // perhaps run a transaction to queue all the queries at once.
                if (DatabaseManager.ShardConfig.BoolExists(i.Key))
                    DatabaseManager.ShardConfig.SaveBool(new Database.Models.Shard.ConfigPropertiesBoolean { Key = i.Key, Value = i.Value.Item, Description = i.Value.Description });
                else
                    DatabaseManager.ShardConfig.AddBool(i.Key, i.Value.Item, i.Value.Description);
            }
        }

        /// <summary>
        /// Writes all of the updated integer values from the cache into the database.
        /// </summary>
        private static void WriteLongToDB()
        {
            foreach (var i in CachedLongSettings.Where(r => r.Value.Modified))
            {
                // todo: see boolean section for caveat in this approach
                if (DatabaseManager.ShardConfig.LongExists(i.Key))
                    DatabaseManager.ShardConfig.SaveLong(new Database.Models.Shard.ConfigPropertiesLong { Key = i.Key, Value = i.Value.Item, Description = i.Value.Description });
                else
                    DatabaseManager.ShardConfig.AddLong(i.Key, i.Value.Item, i.Value.Description);
            }
        }

        /// <summary>
        /// Writes all of the updated float values from the cache into the database.
        /// </summary>
        private static void WriteDoubleToDB()
        {
            foreach (var i in CachedDoubleSettings.Where(r => r.Value.Modified))
            {
                // todo: see boolean section for caveat in this approach
                if (DatabaseManager.ShardConfig.DoubleExists(i.Key))
                    DatabaseManager.ShardConfig.SaveDouble(new Database.Models.Shard.ConfigPropertiesDouble { Key = i.Key, Value = i.Value.Item, Description = i.Value.Description });
                else
                    DatabaseManager.ShardConfig.AddDouble(i.Key, i.Value.Item, i.Value.Description);
            }
        }

        /// <summary>
        /// Writes all of the updated string values from the cache into the database.
        /// </summary>
        private static void WriteStringToDB()
        {
            foreach (var i in CachedStringSettings.Where(r => r.Value.Modified))
            {
                // todo: see boolean section for caveat in this approach
                if (DatabaseManager.ShardConfig.StringExists(i.Key))
                    DatabaseManager.ShardConfig.SaveString(new Database.Models.Shard.ConfigPropertiesString { Key = i.Key, Value = i.Value.Item, Description = i.Value.Description });
                else
                    DatabaseManager.ShardConfig.AddString(i.Key, i.Value.Item, i.Value.Description);
            }
        }

        private static void DoWork(Object source, ElapsedEventArgs e)
        {
            var startTime = DateTime.UtcNow;

            // first, check for variables updated on the server-side. Write those to the DB.
            // then, compare variables to DB and update from DB as necessary. (needs to minimize r/w)

            WriteBoolToDB();
            WriteLongToDB();
            WriteDoubleToDB();
            WriteStringToDB();

            // next, we need to fetch all of the variables from the DB and compare them quickly.
            LoadPropertiesFromDB();

            log.DebugFormat("PropertyManager DoWork took {0:N0} ms", (DateTime.UtcNow - startTime).TotalMilliseconds);
        }
        public static string ListProperties()
        {
            string props = "Boolean properties:\n";
            foreach (var item in DefaultPropertyManager.DefaultBooleanProperties)
                props += string.Format("\t{0}: {1} (current is {2}, default is {3})\n", item.Key, item.Value.Description, GetBool(item.Key).Item, item.Value.Item);

            props += "\nLong properties:\n";
            foreach (var item in DefaultPropertyManager.DefaultLongProperties)
                props += string.Format("\t{0}: {1} (current is {2}, default is {3})\n", item.Key, item.Value.Description, GetLong(item.Key).Item, item.Value.Item);

            props += "\nDouble properties:\n";
            foreach (var item in DefaultPropertyManager.DefaultDoubleProperties)
                props += string.Format("\t{0}: {1} (current is {2}, default is {3})\n", item.Key, item.Value.Description, GetDouble(item.Key).Item, item.Value.Item);

            props += "\nString properties:\n";
            foreach (var item in DefaultPropertyManager.DefaultStringProperties)
                props += string.Format("\t{0}: {1} (default is hidden)\n", item.Key, item.Value.Description);

            return props;
        }
    }

    public struct Property<T>
    {
        public Property(T item, string description) : this()
        {
            Item = item;
            Description = description;
        }

        public T Item { get; }
        public string Description { get; }
    }

    class ConfigurationEntry<T>
    {
        public bool Modified;
        public T Item;
        public string Description;

        public ConfigurationEntry(bool modified, T item)
        {
            Modified = modified;
            Item = item;
        }

        public ConfigurationEntry(bool modified, T item, string description)
        {
            Modified = modified;
            Item = item;
            Description = description;
        }

        public void Modify(T item)
        {
            Item = item;
            Modified = true;
        }

        public void ModifyDescription(string description)
        {
            Description = description;
            Modified = true;
        }

        public override string ToString()
        {
            return Item + " " + Modified;
        }
    }

    public static class DefaultPropertyManager
    {
        private static ReadOnlyDictionary<A,V> DictOf<A, V>()
        {
            return new ReadOnlyDictionary<A, V>(new Dictionary<A, V>());
        }

        private static ReadOnlyDictionary<A, V> DictOf<A, V>(params (A, V)[] pairs)
        {
            return new ReadOnlyDictionary<A, V>(pairs.ToDictionary
            (
                tup => tup.Item1,
                tup => tup.Item2
            ));
        }

        public static void LoadDefaultProperties()
        {
            // Place any default properties to load in here

            //bool
            foreach (var item in DefaultBooleanProperties)
                PropertyManager.ModifyBool(item.Key, item.Value.Item);

            //float
            foreach (var item in DefaultDoubleProperties)
                PropertyManager.ModifyDouble(item.Key, item.Value.Item, true);

            //int
            foreach (var item in DefaultLongProperties)
                PropertyManager.ModifyLong(item.Key, item.Value.Item);

            //string
            foreach (var item in DefaultStringProperties)
                PropertyManager.ModifyString(item.Key, item.Value.Item);
        }

        // ==================================================================================
        // To change these values for the server,
        // please use the /modifybool, /modifylong, /modifydouble, and /modifystring commands
        // ==================================================================================

        public static readonly ReadOnlyDictionary<string, Property<bool>> DefaultBooleanProperties =
            DictOf(
                ("account_login_boots_in_use", new Property<bool>(true, "if FALSE, oldest connection to account is not booted when new connection occurs")),
                ("advanced_combat_pets", new Property<bool>(false, "(non-retail function) If enabled, Combat Pets can cast spells")),
                ("advocate_fane_auto_bestow", new Property<bool>(false, "If enabled, Advocate Fane will automatically bestow new advocates to advocate_fane_auto_bestow_level")),
                ("aetheria_heal_color", new Property<bool>(false, "If enabled, changes the aetheria healing over time messages from the default retail red color to green")),
                ("allow_combat_mode_crafting", new Property<bool>(false, "If enabled, allows players to do crafting (recipes) from all stances. Forces players to NonCombat first, then continues to recipe action.")),
                ("allow_door_hold", new Property<bool>(true, "enables retail behavior where standing on a door while it is closing keeps the door as ethereal until it is free from collisions, effectively holding the door open for other players")),
                ("allow_fast_chug", new Property<bool>(true, "enables retail behavior where a player can consume food and drink faster than normal by breaking animation")),
                ("allow_highres_dat", new Property<bool>(false, "enables client to use highres dat for graphics")),
                ("allow_jump_loot", new Property<bool>(true, "enables retail behavior where a player can quickly loot items while jumping, bypassing the 'crouch down' animation")),
                ("allow_negative_dispel_resist", new Property<bool>(true, "enables retail behavior where #-# negative dispels can be resisted")),
                ("allow_negative_rating_curve", new Property<bool>(true, "enables retail behavior where negative DRR from void dots didn't switch to the reverse rating formula, resulting in a possibly unintended curve that quickly ramps up as -rating goes down, eventually approaching infinity / divide by 0 for -100 rating. less than -100 rating would produce negative numbers.")),
                ("allow_pkl_bump", new Property<bool>(true, "enables retail behavior where /pkl checks for entry collisions, bumping the player position over if standing on another PKLite. This effectively enables /pkl door skipping from retail")),
                ("allow_summoning_killtask_multicredit", new Property<bool>(true, "enables retail behavior where a summoner can get multiple killtask credits from a monster")),
                ("assess_creature_mod", new Property<bool>(false, "(non-retail function) If enabled, re-enables former skill formula, when assess creature skill is not trained or spec'ed")),
                ("attribute_augmentation_safety_cap", new Property<bool>(true, "if TRUE players are not able to use attribute augmentations if the innate value of the target attribute is >= 96. All normal restrictions to these augmentations still apply.")),
                ("chat_disable_general", new Property<bool>(false, "disable general global chat channel")),
                ("chat_disable_lfg", new Property<bool>(false, "disable lfg global chat channel")),
                ("chat_disable_olthoi", new Property<bool>(false, "disable olthoi global chat channel")),
                ("chat_disable_roleplay", new Property<bool>(false, "disable roleplay global chat channel")),
                ("chat_disable_trade", new Property<bool>(false, "disable trade global chat channel")),
                ("chat_echo_only", new Property<bool>(false, "global chat returns to sender only")),
                ("chat_echo_reject", new Property<bool>(false, "global chat returns to sender on reject")),
                ("chat_inform_reject", new Property<bool>(true, "global chat informs sender on reason for reject")),
                ("chat_log_abuse", new Property<bool>(false, "log abuse chat")),
                ("chat_log_admin", new Property<bool>(false, "log admin chat")),
                ("chat_log_advocate", new Property<bool>(false, "log advocate chat")),
                ("chat_log_allegiance", new Property<bool>(false, "log allegiance chat")),
                ("chat_log_audit", new Property<bool>(true, "log audit chat")),
                ("chat_log_debug", new Property<bool>(false, "log debug chat")),
                ("chat_log_fellow", new Property<bool>(false, "log fellow chat")),
                ("chat_log_general", new Property<bool>(false, "log general chat")),
                ("chat_log_global", new Property<bool>(false, "log global broadcasts")),
                ("chat_log_help", new Property<bool>(false, "log help chat")),
                ("chat_log_lfg", new Property<bool>(false, "log LFG chat")),
                ("chat_log_olthoi", new Property<bool>(false, "log olthoi chat")),
                ("chat_log_qa", new Property<bool>(false, "log QA chat")),
                ("chat_log_roleplay", new Property<bool>(false, "log roleplay chat")),
                ("chat_log_sentinel", new Property<bool>(false, "log sentinel chat")),
                ("chat_log_society", new Property<bool>(false, "log society chat")),
                ("chat_log_trade", new Property<bool>(false, "log trade chat")),
                ("chat_log_townchans", new Property<bool>(false, "log advocate town chat")),
                ("chat_requires_account_15days", new Property<bool>(false, "global chat privileges requires accounts to be 15 days or older")),
                ("chess_enabled", new Property<bool>(true, "if FALSE then chess will be disabled")),
                ("use_cloak_proc_custom_scale", new Property<bool>(false, "If TRUE, the calculation for cloak procs will be based upon the values set by the server oeprator.")),
                ("client_movement_formula", new Property<bool>(false, "If enabled, server uses DoMotion/StopMotion self-client movement methods instead of apply_raw_movement")),
                ("container_opener_name", new Property<bool>(false, "If enabled, when a player tries to open a container that is already in use by someone else, replaces 'someone else' in the message with the actual name of the player")),
                ("corpse_decay_tick_logging", new Property<bool>(false, "If ENABLED then player corpse ticks will be logged")),
                ("corpse_destroy_pyreals", new Property<bool>(false, "If FALSE then pyreals will not be completely destroyed on player death")),   // Project ACX: dropped pyreals stay on the corpse (was true)
                ("craft_exact_msg", new Property<bool>(false, "If TRUE, and player has crafting chance of success dialog enabled, shows them an additional message in their chat window with exact %")),
                ("creature_name_check", new Property<bool>(true, "if enabled, creature names in world database restricts player names during character creation")),
                ("creatures_drop_createlist_wield", new Property<bool>(false, "If FALSE then Wielded items in CreateList will not drop. Retail defaulted to TRUE but there are currently data errors")),
                ("fastbuff", new Property<bool>(true, "If TRUE, enables the fast buffing trick from retail.")),
                ("fellow_busy_no_recruit", new Property<bool>(true, "if FALSE, fellows can be recruited while they are busy, different from retail")),
                ("fellow_kt_killer", new Property<bool>(true, "if FALSE, fellowship kill tasks will share with the fellowship, even if the killer doesn't have the quest")),
                ("fellow_kt_landblock", new Property<bool>(false, "if TRUE, fellowship kill tasks will share with landblock range (192 distance radius, or entire dungeon)")),
                ("fellow_quest_bonus", new Property<bool>(false, "if TRUE, applies EvenShare formula to fellowship quest reward XP (300% max bonus, defaults to false in retail)")),
                ("fix_chest_missing_inventory_window", new Property<bool>(false, "Very non-standard fix. This fixes an acclient bug where unlocking a chest, and then quickly opening it before the client has received the Locked=false update from server can result in the chest opening, but with the chest inventory window not displaying. Bug has a higher chance of appearing with more network latency.")),
                ("gateway_ties_summonable", new Property<bool>(true, "if disabled, players cannot summon ties from gateways. defaults to enabled, as in retail")),
                ("gearknight_core_plating", new Property<bool>(true, "if disabled, Gear Knight players are not required to use core plating devices for armor and clothing. defaults to enabled, as in retail")),
                ("house_15day_account", new Property<bool>(true, "if disabled, houses can be purchased with accounts created less than 15 days old")),
                ("house_30day_cooldown", new Property<bool>(true, "if disabled, houses can be purchased without waiting 30 days between each purchase")),
                ("house_hook_limit", new Property<bool>(true, "if disabled, house hook limits are ignored")),
                ("house_hookgroup_limit", new Property<bool>(true, "if disabled, house hook group limits are ignored")),
                ("house_per_char", new Property<bool>(false, "if TRUE, allows 1 house per char instead of 1 house per account")),
                ("house_purchase_requirements", new Property<bool>(true, "if disabled, requirements to purchase/rent house are not checked")),
                ("house_rent_enabled", new Property<bool>(true, "If FALSE then rent is not required")),
                ("iou_trades", new Property<bool>(false, "(non-retail function) If enabled, IOUs can be traded for objects that are missing in DB but added/restored later on")),
                ("item_dispel", new Property<bool>(false, "if enabled, allows players to dispel items. defaults to end of retail, where item dispels could only target creatures")),
                ("lifestone_broadcast_death", new Property<bool>(true, "if true, player deaths are additionally broadcast to other players standing near the destination lifestone")),
                ("loot_quality_mod", new Property<bool>(true, "if FALSE then the loot quality modifier of a Death Treasure profile does not affect loot generation")),
                ("npc_hairstyle_fullrange", new Property<bool>(false, "if TRUE, allows generated creatures to use full range of hairstyles. Retail only allowed first nine (0-8) out of 51")),
                ("offline_xp_passup_limit", new Property<bool>(true, "if FALSE, allows unlimited xp to passup to offline characters in allegiances")),
                ("olthoi_play_disabled", new Property<bool>(false, "if false, allows players to create and play as olthoi characters")),
                ("override_encounter_spawn_rates", new Property<bool>(false, "if enabled, landblock encounter spawns are overidden by double properties below.")),
                ("allow_xp_at_max_level", new Property<bool>(true, "Project ACX: players keep earning experience at max level (ClassicACE)")),
                ("fall_damage_enabled", new Property<bool>(true, "Project ACX: toggles fall damage (ClassicACE)")),
                ("unlimited_respec", new Property<bool>(true, "Project ACX: no quest timer on the skill and attribute reset gems up to unlimited_respec_max_level (ClassicACE)")),
                ("allow_allegiance_passup", new Property<bool>(true, "Project ACX: toggles allegiance experience pass-up (ClassicACE)")),
                ("acx_patron_level_rule", new Property<bool>(true, "Project ACX: a patron must be your level or higher to swear to them (ClassicACE, while pass-up is on)")),
                ("equipmentsetid_enabled", new Property<bool>(true, "Project ACX: armor sets can appear on loot armor (ClassicACE)")),
                ("equipmentsetid_name_decoration", new Property<bool>(false, "Project ACX: put the armor set name in front of loot armor names (ClassicACE)")),
                ("acx_ghosts", new Property<bool>(true, "Project ACX: if TRUE, players from level 10 who die (not to a player, not in an arena) wait at their corpse as a ghost for up to 5 minutes, and can be revived with a revive scroll")),
                ("acx_attack_trace", new Property<bool>(false, "Project ACX: if TRUE, the server log gets a line for each missile attack request, throw, and why a repeat stopped ([ACXTRACE]; JJ 2026-11-19, UCM slow to shoot). A diagnostic: leave off")),
                ("acx_one_click_heal", new Property<bool>(true, "Project ACX: double-clicking a healing, stamina or mana kit heals the selected player, or yourself (ClassicACE one-click healing); false gives kits their targeting cursor back")),
                ("acx_tar_enabled", new Property<bool>(true, "Project ACX: T.A.R. camps (ClassicACE): a kill pays a third of its experience per camp (its creature Type, the Area, and Rest), and each camp pays less the more it has been farmed recently, recovering over time, online or off. /tar shows yours")),
                ("acx_contract_progress_message", new Property<bool>(false, "Project ACX: a chat line on each kill toward a kill contract (off: chat stays quiet, /contract shows progress)")),
                ("acx_craft_requirement_warning", new Property<bool>(true, "Project ACX: the craft confirmation also warns when you could not wield or activate the item a recipe makes (from Dekaru's ClassicACE)")),
                ("acx_leaderboards_include_admins", new Property<bool>(false, "Project ACX: PREVIEW ONLY, for testing: admins count on the leaderboards and in wealth ranks. Keep FALSE on Live (JJ: admins excluded)")),
                ("acx_alchemist_enabled", new Property<bool>(true, "Project ACX (on since 2026-11-17, JJ: \"yes ship it to live\"): the Alchemist (JJ 2026-11-16; AcxBombWcids): bombs drop as loot and the Mark Trader sells the Healing bomb instead of its nine eternal phials. Off until JJ turns it on")),
                ("acx_economy_log_admins", new Property<bool>(false, "Project ACX: the economy log (Logs/acx-economy.csv) also writes admin characters' sessions (on for testing on Preview)")),
                ("acx_invasions_enabled", new Property<bool>(true, "Project ACX: elite town invasions (Managers/AcxInvasions.cs): every acx_invasion_min_minutes to acx_invasion_max_minutes a random town from Content/invasions/towns.json is warned, then attacked by an elite")),
                ("acx_vendor_bank", new Property<bool>(true, "Project ACX: vendors trade through the bank (JJ 2026-11-21: \"Most money will be in bank\", \"sales going directly into the bank would be pretty awesome\"): a pyreal purchase is paid from the bank first, the pack covering what the bank can't, and a sale's pyreals go into the bank instead of the pack")),
                ("acx_imbue_failure_destroys", new Property<bool>(false, "Project ACX: a failed imbue destroys the weapon, as in retail; off (JJ 2026-11-17), the salvage is used up and the weapon kept, its tinker count unchanged. Tinkering keeps retail's risk either way. Quest and named weapons: acx_quest_imbue_failure_destroys")),
                ("acx_quest_imbue_failure_destroys", new Property<bool>(false, "Project ACX: a failed imbue on an untinkerable quest or named weapon (they take one imbue) destroys it, as on loot; off, the gem is used up and the weapon kept")),
                ("acx_tar_kill_message", new Property<bool>(false, "Project ACX: with acx_tar_enabled, a kill that pays less than full experience shows its T: A: R: multipliers (ClassicACE adds them to every kill experience message)")),
                ("acx_tar_turnin_enabled", new Property<bool>(true, "Project ACX: with acx_tar_enabled, each item handed to an NPC counts on its own turn-in camp, which scales the items given back: x(1 - recent turn-ins of that item / acx_tar_turnin_interactions), rounded up, nothing once it reaches 0 (ClassicACE)")),
                ("acx_tar_turnin_xp", new Property<bool>(true, "Project ACX: with acx_tar_turnin_enabled, the turn-in camp scales the quest experience a turn-in pays as well: each recent turn-in of the same item pays 1/acx_tar_turnin_interactions less (collectors, crafters, trophy smiths and gambling staff exempt). On since 2026-11-17, with XP back at retail (JJ: \"Yes lets turn it on we can always turn it off\"); ClassicACE ships it off")),
                ("stackable_trophy_rewards_use_tar", new Property<bool>(false, "Project ACX: with acx_tar_turnin_enabled, stackable rewards for a trophy also scale from 120% down to 20% with that trophy's turn-in camp, on top of the turn-in scaling (ClassicACE)")),
                ("pathfinding", new Property<bool>(false, "Project ACX: if TRUE, monsters in dungeons follow navmesh routes around walls to reach their target (ported from ClassicACE). Prebuild the meshes with /acxmeshbuild all")),
                ("permit_corpse_all", new Property<bool>(false, "If TRUE, /permit grants permittees access to all corpses of the permitter. Defaults to FALSE as per retail, where /permit only grants access to 1 locked corpse")),
                ("persist_movement", new Property<bool>(false, "If TRUE, persists autonomous movements such as turns and sidesteps through non-autonomous server actions. Retail didn't appear to do this, but some players may prefer this.")),
                ("pet_stow_replace", new Property<bool>(false, "pet stowing for different pet devices becomes a stow and replace. defaults to retail value of false")),
                ("player_config_command", new Property<bool>(false, "If enabled, players can use /config to change their settings via text commands")),
                ("player_receive_immediate_save", new Property<bool>(false, "if enabled, when the player receives items from an NPC, they will be saved immediately")),
                ("pk_server", new Property<bool>(false, "set this to TRUE for darktide servers")),
                ("pk_server_safe_training_academy", new Property<bool>(false, "set this to TRUE to disable pk fighting in training academy and time to exit starter town safely")),
                ("pkl_server", new Property<bool>(false, "set this to TRUE for pink servers")),
                ("quest_info_enabled", new Property<bool>(true, "toggles the /myquests player command (Project ACX: on, JJ 2026-11-16: \"turn on the my quests on live. No reason not to\"; the Kill Task Tracker plugin reads it)")),
                ("rares_real_time", new Property<bool>(true, "allow for second chance roll based on an rng seeded timestamp for a rare on rare eligible kills that do not generate a rare, rares_max_seconds_between defines maximum seconds before second chance kicks in")),
                ("rares_real_time_v2", new Property<bool>(false, "chances for a rare to be generated on rare eligible kills are modified by the last time one was found per each player, rares_max_days_between defines maximum days before guaranteed rare generation")),
                ("runrate_add_hooks", new Property<bool>(false, "if TRUE, adds some runrate hooks that were missing from retail (exhaustion done, raise skill/attribute")),
                ("reportbug_enabled", new Property<bool>(false, "toggles the /reportbug player command")),
                ("require_spell_comps", new Property<bool>(true, "if FALSE, spell components are no longer required to be in inventory to cast spells. defaults to enabled, as in retail")),
                ("safe_spell_comps", new Property<bool>(false, "if TRUE, disables spell component burning for everyone")),
                ("salvage_handle_overages", new Property<bool>(false, "in retail, if 2 salvage bags were combined beyond 100 structure, the overages would be lost")),
                ("show_ammo_buff", new Property<bool>(false, "shows active enchantments such as blood drinker on equipped missile ammo during appraisal")),
                ("show_aura_buff", new Property<bool>(false, "shows active aura enchantments on wielded items during appraisal")),
                ("show_dat_warning", new Property<bool>(false, "if TRUE, will alert player (dat_warning_msg) when client attempts to download from server and boot them from game, disabled by default")),
                ("show_dot_messages", new Property<bool>(false, "enabled, shows combat messages for DoT damage ticks. defaults to disabled, as in retail")),
                ("show_first_login_gift", new Property<bool>(false, "if TRUE, will show on first login that the player earned bonus item (Blackmoor's Favor and/or Asheron's Benediction), disabled by default because msg is kind of odd on an emulator")),
                ("show_mana_conv_bonus_0", new Property<bool>(true, "if disabled, only shows mana conversion bonus if not zero, during appraisal of casting items")),
                ("smite_uses_takedamage", new Property<bool>(false, "if enabled, smite applies damage via TakeDamage")),
                ("spellcast_recoil_queue", new Property<bool>(false, "if true, players can queue the next spell to cast during recoil animation")),
                ("spell_projectile_ethereal", new Property<bool>(false, "broadcasts all spell projectiles as ethereal to clients only, and manually send stop velocity on collision. can fix various issues with client missing target id.")),
                ("suicide_instant_death", new Property<bool>(false, "if enabled, @die command kills player instantly. defaults to disabled, as in retail")),
                ("taboo_table", new Property<bool>(true, "if enabled, taboo table restricts player names during character creation")),
                ("tailoring_intermediate_uieffects", new Property<bool>(false, "If true, tailoring intermediate icons retain the magical/elemental highlight of the original item")),
                ("trajectory_alt_solver", new Property<bool>(false, "use the alternate trajectory solver for missiles and spell projectiles")),
                ("universal_masteries", new Property<bool>(true, "if TRUE, matches end of retail masteries - players wielding almost any weapon get +5 DR, except if the weapon \"seems tough to master\". " +
                                                                 "if FALSE, players start with mastery of 1 melee and 1 ranged weapon type based on heritage, and can later re-select these 2 masteries")),
                ("unlimited_sequence_gaps", new Property<bool>(false, "upon startup, allows server to find all unused guids in a range instead of a set hard limit")),
                ("use_generator_rotation_offset", new Property<bool>(true, "enables or disables using the generator's current rotation when offseting relative positions")),
                ("use_portal_max_level_requirement", new Property<bool>(true, "disable this to ignore the max level restriction on portals")),
                ("use_turbine_chat", new Property<bool>(true, "enables or disables global chat channels (General, LFG, Roleplay, Trade, Olthoi, Society, Allegience)")),
                ("use_wield_requirements", new Property<bool>(true, "disable this to bypass wield requirements. mostly for dev debugging")),
                ("version_info_enabled", new Property<bool>(false, "toggles the /aceversion player command")),
                ("vendor_shop_uses_generator", new Property<bool>(false, "enables or disables vendors using generator system in addition to createlist to create artificial scarcity")),
                ("world_closed", new Property<bool>(false, "enable this to startup world as a closed to players world"))
                );

        public static readonly ReadOnlyDictionary<string, Property<long>> DefaultLongProperties =
            DictOf(
                ("acx_vault_rent_days", new Property<long>(30, "Project ACX: days of rent a Vault Deed or Vault Rent Writ pays for a Haven vault (prices are the items' values at the Vault Keeper)")),
                ("acx_vault_max_prepaid_days", new Property<long>(180, "Project ACX: how far ahead a Haven vault's rent can be paid")),
                ("acx_vault_per_account", new Property<long>(2, "Project ACX: vaults one account can lease (0 = no limit). 2 since 2026-11-16 (JJ: \"Yes set it to two\"; the ACX Guide tells players they may lease up to two)")),
                ("acx_vault_item_slots", new Property<long>(102, "Project ACX: item slots in a Haven vault's main compartment (a main pack is 102)")),
                ("acx_vault_bag_slots", new Property<long>(7, "Project ACX: bag slots in a Haven vault (a character has 7)")),
                ("acx_economy_log_days", new Property<long>(30, "Project ACX: days the economy log (Logs/acx-economy.csv: Marks, pyreals and XP per play session) runs, from the first line written (acx_economy_log_started); 0 = off (JJ 2026-11-17: \"lets do that for one month\")")),
                ("acx_invasion_min_minutes", new Property<long>(90, "Project ACX: shortest time between elite town invasions, in minutes (random between this and acx_invasion_max_minutes)")),
                ("acx_invasion_max_minutes", new Property<long>(120, "Project ACX: longest time between elite town invasions, in minutes")),
                ("acx_invasion_min_players", new Property<long>(1, "Project ACX: an invasion only starts with at least this many players online")),
                ("acx_invasion_warning_minutes", new Property<long>(5, "Project ACX: minutes between the world-wide warning and the invader's arrival")),
                ("acx_invasion_duration_minutes", new Property<long>(30, "Project ACX: an invader nobody kills leaves after this many minutes")),
                ("acx_invasion_corpse_marks", new Property<long>(10, "Project ACX: ACX Marks on a slain invader's corpse; anyone who reaches it may take one (JJ: one per event)")),
                ("acx_invasion_corpse_minutes", new Property<long>(15, "Project ACX: a slain invader's corpse lasts this many minutes and is open to everyone")),
                ("acx_mark_wcid", new Property<long>(900710, "Project ACX: the ACX Mark currency (weenie class id); 0 = none. One per event: exploration marker, contract, job, invader corpse")),
                ("acx_town_headstone_minutes", new Property<long>(60, "Project ACX: a headstone inside a town (Content/invasions/towns.json) fades after this many minutes; elsewhere they stay")),
                ("acx_fishing_mob_minutes", new Property<long>(5, "Project ACX: a creature pulled from a fishing hole leaves after this many minutes if nobody kills it")),
                ("acx_elite_loot_count", new Property<long>(20, "Project ACX: items an elite drops (ClassicACE's elite_mob_loot_count)")),
                ("acx_elite_xp_per_mod", new Property<long>(100000, "Project ACX: an elite of level 100 or under gives this much experience per mod, shared by damage (ClassicACE)")),
                ("acx_elite_xp_per_mod_high", new Property<long>(2000000, "Project ACX: an elite over level 100 gives this much experience per mod, shared by damage (ClassicACE)")),
                ("acx_vault_landblock", new Property<long>(0x018A, "Project ACX: the landblock the vaults stand in, for /vault and /acxvault (0x018A = 394: Concordia)")),
                ("acx_vault_grace_days", new Property<long>(0, "Project ACX: days after the rent falls due before a Haven vault's lease is lost (the vault and everything in it go to the next deed)")),
                ("acx_vault_rent_warning_days", new Property<long>(3, "Project ACX: the leaseholder is told the due date on opening their vault when rent is due within this many days")),
                ("acx_alchemist_imperil_max_level", new Property<long>(3, "Project ACX: the highest Imperil level (1-6: -20 to -200 armor) an Alchemist's Nether bomb puts on, whatever its tier (its debuff; AcxBombWcids). 3 (-75): at 6 the Nether bomb was the best bomb on 15 of 22 end-game monsters; at 3 it is on the nether-weak ones")),
                ("acx_alchemist_splash_targets", new Property<long>(3, "Project ACX: the most other monsters a bomb's blast hits, the nearest to its target, each for acx_alchemist_splash_share of a hit (JJ 2026-11-16: \"I might end up putting a max of 3 monsters hit by splash damage\"). 0 = no limit. 3 x 0.43: a blast is worth at most 2.3 hits, as before (until 2026-11-16 night: 2 at 0.65, a bigger crowd sharing those 1.3 hits)")),
                ("acx_contract_marks", new Property<long>(2, "Project ACX: ACX Marks paid for each finished contract (2 since 2026-11-16, JJ: \"bump them to two marks per run\"; was 1)")),
                ("acx_fellowship_level_cap", new Property<long>(180, "Project ACX: fellowships count anyone above this level as this level (JJ 2026-11-14)")),
                ("acx_fellowship_band_size", new Property<long>(25, "Project ACX: below acx_fellowship_top_level, fellows in the same band of this many levels share XP evenly at any gap (1-25, 26-50 ... 151-180; JJ 2026-11-19)")),
                ("acx_fellowship_gap", new Property<long>(5, "Project ACX: below acx_fellowship_top_level, fellows within this many levels share XP evenly across a band edge (JJ 2026-11-19: a 25 with up to a 30, not a 31)")),
                ("acx_fellowship_top_level", new Property<long>(181, "Project ACX: fellows all at this level or higher share XP evenly within acx_fellowship_top_gap levels; below it, within acx_fellowship_gap (JJ 2026-11-19)")),
                ("acx_fellowship_top_gap", new Property<long>(20, "Project ACX: from acx_fellowship_top_level, fellows within this many levels share XP evenly (JJ 2026-11-19)")),
                ("acx_nether_weapon_min_tier", new Property<long>(1, "Project ACX: lowest loot tier that can drop nether versions of weapons")),
                ("char_delete_time", new Property<long>(3600, "the amount of time in seconds a deleted character can be restored")),
                ("chat_requires_account_time_seconds", new Property<long>(0, "the amount of time in seconds an account is required to have existed for for global chat privileges")),
                ("chat_requires_player_age", new Property<long>(0, "the amount of time in seconds a player is required to have played for global chat privileges")),
                ("chat_requires_player_level", new Property<long>(0, "the level a player is required to have for global chat privileges")),
                ("corpse_spam_limit", new Property<long>(15, "the number of corpses a player is allowed to leave on a landblock at one time")),
                ("default_subscription_level", new Property<long>(1, "retail defaults to 1, 1 = standard subscription (same as 2 and 3), 4 grants ToD pre-order bonus item Asheron's Benediction")),
                ("fellowship_even_share_level", new Property<long>(50, "level when fellowship XP sharing is no longer restricted")),
                ("max_level", new Property<long>(275, "Project ACX: the max character level (ClassicACE); can't exceed the client's XP table (275)")),
                ("unlimited_respec_max_level", new Property<long>(20, "Project ACX: the highest level that gets unlimited respec (ClassicACE)")),
                ("quest_mindelta_rate_shortest", new Property<long>(72000, "Project ACX: scaled quest timers (quest_mindelta_rate) never go below this many seconds, and shorter ones aren't scaled (ClassicACE)")),
                ("quest_mindelta_rate_longest", new Property<long>(0, "Project ACX: quest timers longer than this many seconds are cut to it; 0 = no cap (ClassicACE uses 600000)")),
                ("acx_extra_mutation_min_tier", new Property<long>(2, "Project ACX: the lowest loot tier for extra weapon mutations (Shield/Armor/Resistance Cleaving, Biting Strike, Crushing Blow) and shields' Magic Absorbing; slayers roll at any tier")),
                ("acx_tar_type_interactions", new Property<long>(2000, "Project ACX: T.A.R.: interactions (kills, weighted by health) on one creature type before its Type camp pays nothing (ClassicACE)")),
                ("acx_tar_area_interactions", new Property<long>(750, "Project ACX: T.A.R.: interactions in one area (a dungeon landblock, or a 16x16-landblock square outdoors) before its Area camp pays nothing (ClassicACE)")),
                ("acx_tar_rest_interactions", new Property<long>(500, "Project ACX: T.A.R.: interactions anywhere before the Rest camp pays nothing (ClassicACE)")),
                ("acx_tar_turnin_interactions", new Property<long>(10, "Project ACX: T.A.R.: turn-ins of one item before its turn-in camp gives nothing back (ClassicACE)")),
                ("acx_tar_health_per_interaction", new Property<long>(1000, "Project ACX: T.A.R.: a kill counts 1 + (the health the creature lost / this) interactions on each camp, at most 100 (ClassicACE)")),
                ("mansion_min_rank", new Property<long>(0, "overrides the default allegiance rank required to own a mansion")),   // Project ACX: no rank requirement on villas or mansions (was 6)
                ("max_chars_per_account", new Property<long>(11, "retail defaults to 11, client supports up to 20")),
                ("pk_timer", new Property<long>(20, "the number of seconds where a player cannot perform certain actions (ie. teleporting) after becoming involved in a PK battle")),
                ("player_save_interval", new Property<long>(300, "the number of seconds between automatic player saves")),
                ("rares_max_days_between", new Property<long>(45, "for rares_real_time_v2: the maximum number of days a player can go before a rare is generated on rare eligible creature kills")),
                ("rares_max_seconds_between", new Property<long>(5256000, "for rares_real_time: the maximum number of seconds a player can go before a second chance at a rare is allowed on rare eligible creature kills that did not generate a rare")),
                ("summoning_killtask_multicredit_cap", new Property<long>(2, "if allow_summoning_killtask_multicredit is enabled, the maximum # of killtask credits a player can receive from 1 kill")),
                ("teleport_visibility_fix", new Property<long>(0, "Fixes some possible issues with invisible players and mobs. 0 = default / disabled, 1 = players only, 2 = creatures, 3 = all world objects"))
                );

        public static readonly ReadOnlyDictionary<string, Property<double>> DefaultDoubleProperties =
            DictOf(

                ("acx_rare_weapon_chance_mod", new Property<double>(50, "Project ACX: multiplies the chance that a rare is a tier 6 (weapon) rare, retail 1 in 3,500 rares. 50 (JJ 2026-11-16): about one rare weapon per 10 months of play for one player at ~200 eligible kills an hour, 3 hours a day; 1 = retail. Other rare tiers and rare_drop_rate_percent are unchanged.")),
                ("acx_bleed_damage_scale", new Property<double>(0.15, "Project ACX: scales dagger and multi-strike sword bleeds outside PvP. 0.15 locked in the 2026-11-13 balance pass (/acxbench): bleeders a little over the pack instead of 35-75% (Pourkarutide ran 0.5, ClassicACE 1.0)")),
                ("fall_damage_multiplier", new Property<double>(1.0, "Project ACX: scales fall damage (ClassicACE)")),
                ("mob_awareness_range_indoors", new Property<double>(1.0, "Project ACX: scales how far monsters notice players inside dungeons (ClassicACE)")),
                ("acx_mob_damage_scale", new Property<double>(1.0, "Project ACX: scales monster melee and missile damage against players (ClassicACE customdm_mob_damage_scale)")),
                ("acx_light_weapons_damage_mod", new Property<double>(1.135, "Project ACX: players' Light Weapons damage multiplier, so Light sits about 5% under Heavy (JJ 2026-11-13, measured with /acxbench; retail is 1.0)")),
                ("acx_finesse_weapons_damage_mod", new Property<double>(1.113, "Project ACX: players' Finesse Weapons damage multiplier, so Finesse sits about 5% under Heavy (JJ 2026-11-13, measured with /acxbench; retail is 1.0)")),
                ("acx_lf_multistrike_damage_bonus", new Property<double>(1, "Project ACX: extra damage per strike for players' Light and Finesse multi-strike weapons; their loot tables start at 1 damage where Heavy's start at 4. 1 after the buffed check (3 put them 12 points over Heavy's multi-strike with Blood Drinker); JJ 2026-11-13, retail 0")),
                ("acx_heavy_axe_variance_mod", new Property<double>(0.58, "Project ACX: scales the damage variance Heavy axes roll as loot (retail ~0.87 left them about 23% under the class; JJ 2026-11-13 wants it fixed visibly; 1.0 = retail)")),
                ("acx_light_axe_variance_mod", new Property<double>(0.67, "Project ACX: scales the damage variance Light axes roll as loot (JJ 2026-11-13; 1.0 = retail)")),
                ("acx_spear_variance_mod", new Property<double>(0.85, "Project ACX: scales the damage variance one-handed spears roll as loot, a nudge (JJ 2026-11-13; ~0.7 left spears 2-11% under their class; 1.0 = retail)")),
                ("acx_finesse_axe_variance_mod", new Property<double>(0.73, "Project ACX: scales the damage variance Finesse axes roll as loot (JJ 2026-11-13; 1.0 = retail)")),
                ("acx_mob_war_damage_scale", new Property<double>(1.0, "Project ACX: scales monster war magic damage against players (ClassicACE customdm_mob_war_damage_scale)")),
                ("equipmentsetid_drop_rate", new Property<double>(1.0, "Project ACX: scales how often loot armor carries an armor set (ClassicACE)")),
                ("surface_bonus_xp", new Property<double>(0.25, "Project ACX: extra kill experience outside dungeons; 0.25 = 25% more (ClassicACE)")),
                ("acx_tar_decay_delay", new Property<double>(120, "Project ACX: T.A.R.: seconds after a camp was last used before it starts to recover (ClassicACE)")),
                ("acx_tar_decay_seconds", new Property<double>(300, "Project ACX: T.A.R.: seconds to recover one interaction on Type, Area and turn-in camps (ClassicACE)")),
                ("acx_tar_rest_decay_seconds", new Property<double>(3, "Project ACX: T.A.R.: seconds to recover one interaction on the Rest camp (ClassicACE)")),
                ("allegiance_passup_multiplier", new Property<double>(1.0, "Project ACX: multiplies allegiance pass-up experience; 3.0 = ClassicACE's 3x pass-up")),
                ("acx_melee_reach", new Property<double>(1.5, "Project ACX: how far from a target (metres, edge to edge) a player's melee swing starts without running in first. ACE's 4 relies on the client sliding the attacker in, which AC:Unreal doesn't do for your own character")),
                ("acx_extra_target_damage", new Property<double>(0.5, "Project ACX: damage multiplier for the extra targets of a cleave or a two-handed spear's pierce (the main target takes the full hit)")),
                ("acx_nether_resist_default", new Property<double>(0.86, "Project ACX: nether damage multiplier for monsters without a retail nether resistance (retail placeholder 1.0 counts as none)")),
                ("acx_nether_resist_endgame", new Property<double>(1.25, "Project ACX: nether damage multiplier for Rynthid and Obsidian Plains monsters without a retail nether resistance")),
                ("acx_nether_weapon_chance", new Property<double>(0.0225, "Project ACX: chance a looted weapon that comes in elements is its nether version (0.0225 = 2.25%; other elements get 15% each)")),
                ("acx_dual_wield_follow_up", new Property<double>(0.2, "Project ACX: on every dual-wield attack the other hand's weapon also strikes for this share of a normal hit (0 = off). 0.2 (JJ 2026-11-16: \"if you can kill it fast enough your defenses don't matter as much\"): about +23-25% damage over the sword alone at equal survival, mid-pack among multi-strike weapons (/acxsurvival)")),
                ("acx_single_weapon_defense_bonus", new Property<double>(0.10, "Project ACX: melee and missile defense bonus for a one-handed melee weapon with an empty off-hand (0.10 = +10%)")),
                ("acx_single_weapon_speed_bonus", new Property<double>(0.15, "Project ACX: swing and power bar refill speed bonus for a one-handed melee weapon with an empty off-hand (dual wield gets 0.20)")),
                ("acx_fishing_sack_chance", new Property<double>(0.30, "Project ACX: chance a creature pulled from a fishing hole drops a salvage sack sized by the fisher's level")),
                ("acx_fishing_pearl_chance", new Property<double>(0.15, "Project ACX: chance a creature pulled from a fishing hole drops a pearl (by the fisher's level) that the Tackle Master buys")),
                ("acx_fishing_creature_mark_chance", new Property<double>(0.10, "Project ACX: chance a creature pulled from a fishing hole drops an ACX Mark, at most once a day for the fisher")),
                ("acx_fishing_mob_level", new Property<double>(0.5, "Project ACX: a creature pulled from a fishing hole is about this fraction of the fisher's level (JJ: half, so fishing is safe unbuffed)")),
                ("acx_fishing_mob_chance", new Property<double>(0.01, "Project ACX: chance a cast at a fishing hole pulls out a water creature near the fisher's level (casts come at most every 5 seconds)")),
                ("acx_alchemist_loot_share", new Property<double>(0.5, "Project ACX: share of the AcxThrown weapon drops (WeaponTypeChance) that are Alchemist bombs instead of thrown weapons (0.5: bombs as often as thrown, each as often as a bow; JJ 2026-11-21, was 0.125, one thrown family's); needs acx_alchemist_enabled")),
                ("acx_alchemist_skill_damage", new Property<double>(0.004, "Project ACX: an Alchemist bomb's damage multiplier from the thrower's Alchemy, in place of Strength: 1 + (Alchemy - 55) x this (JJ: \"the blast is in the alchemy\"). 0.004: a maxed L275 Alchemist does about 0.9 of a maxed archer's damage to one monster (2026-11-16 /acxsurvival)")),
                ("acx_loot_value_mod", new Property<double>(0.1, "Project ACX: what new loot is worth, as a share of retail's value (coins left alone). Monsters drop no coins, so selling loot is where pyreals come from; 0.1 = 90% less (JJ 2026-11-17: \"its easier to raise then lower lets start at 90%\"). Live: raise it as the economy log shows")),
                ("acx_concordia_buy_rate", new Property<double>(0.4, "Project ACX: the share of an item's value vendors in Concordia (the gambling hall's Pawn Shopkeep) pay: flat, outside vendor reputation, under the 50% every town starts at, so the hub is a fallback for full bags rather than the place to sell (JJ 2026-11-17: \"reduce his % a bit to discourage making him the main\")")),
                ("acx_alchemist_splash_radius", new Property<double>(3.5, "Project ACX: metres around an Alchemist bomb's target that its blast reaches")),
                ("acx_alchemist_splash_share", new Property<double>(0.43, "Project ACX: share of a bomb hit that each other monster in the blast takes (each rolls its own Missile Defense, like cleave), for up to acx_alchemist_splash_targets of them (AcxBombWcids.SplashTargets). 0.43 since 2026-11-16 night (JJ: \"yeah lets lower the share of splash\"; was 0.65)")),
                ("acx_alchemist_throw_speed", new Property<double>(1.25, "Project ACX: an Alchemist bomb's throw (aim and reload) animation speed multiplier, on top of Quickness. 1.25: 2.67 s a throw at L275 against 2.84 for other thrown weapons")),
                ("acx_thrown_shield_speed", new Property<double>(0.65, "Project ACX: a thrown weapon's (and an Alchemist bomb's) aim and reload animation speed with a shield in the other hand (JJ 2026-11-16: \"slow the throws with shields for the throwing class\"). 0.65: a throw takes about 15% longer (2.84 s to 3.3 s), so the shield costs a thrower the 13% of damage it costs a swordsman, who gives up the single-weapon speed. 1 = no cost")),
                ("acx_alchemist_heal_radius", new Property<double>(5.0, "Project ACX: metres around a Healing bomb's impact in which players and pets heal and monsters fester")),
                ("acx_alchemist_heal_per_skill", new Property<double>(0.7, "Project ACX: a Healing bomb's heal over time (12.5 s) per point of the thrower's Alchemy, per player or pet in the burst. 0.7: about a Treated Healing Kit's average at L275 (416 at Alchemy 594)")),
                ("acx_elite_regen_scale", new Property<double>(0.5, "Project ACX: scales elites' health regeneration (HealthRate 100, or 50 when immune to non-projectile magic). 0.5 since 2026-11-16 (JJ: drop it by 50%; target: two players of the town's level beat its elite, one usually can't). Applies to elites made after a change.")),
                ("acx_elite_attribute_bonus_min", new Property<double>(0.1, "Project ACX: an elite's attributes each rise by a rolled share of themselves, from this (0.1 = +10%)... (was ClassicACE's flat +25-75 under level 100, +70-200 from 100; 2026-11-16)")),
                ("acx_elite_attribute_bonus_max", new Property<double>(0.3, "Project ACX: ...to this (0.3 = +30%; 0.1-0.3 picked by the 2026-11-16 /acxsurvival duo sweep)")),
                ("acx_elite_health_scale", new Property<double>(0.5, "Project ACX: scales an elite's health multiplier (x12, x17 from level 100, Beefy x21 / x30 / x15 over 100,000 health), at least x1. 0.5 since 2026-11-16 (target: two players of the town's level beat its elite, one usually can't)")),
                ("acx_elite_damage_mod", new Property<double>(2.0, "Project ACX: elites' melee and missile damage multiplier (ClassicACE: double)")),
                ("acx_elite_loot_quality", new Property<double>(0.5, "Project ACX: loot quality mod of an elite's drop (ClassicACE's elite_mob_loot_quality; most retail tables use 0, boss tables 0.25)")),
                ("acx_invasion_awareness", new Property<double>(30, "Project ACX: an idle invader wakes for any player within this many metres, even one standing still")),
                ("acx_twohanded_crit_bonus", new Property<double>(0.05, "Project ACX: extra critical chance for players attacking with two-handed weapons")),
                ("cantrip_drop_rate", new Property<double>(1.0, "Scales the chance for cantrips to drop in each tier. Defaults to 1.0, as per end of retail")),
                ("cloak_cooldown_seconds", new Property<double>(5.0, "The number of seconds between possible cloak procs.")),
                ("cloak_max_proc_base", new Property<double>(0.25, "The max proc chance of a cloak.")),
                ("cloak_max_proc_damage_percentage", new Property<double>(0.30, "The damage percentage at which cloak proc chance plateaus.")),
                ("cloak_min_proc", new Property<double>(0, "The minimum proc chance of a cloak.")),
                ("minor_cantrip_drop_rate", new Property<double>(1.0, "Scales the chance for minor cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail")),
                ("major_cantrip_drop_rate", new Property<double>(1.0, "Scales the chance for major cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail")),
                ("epic_cantrip_drop_rate", new Property<double>(1.0, "Scales the chance for epic cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail")),
                ("legendary_cantrip_drop_rate", new Property<double>(1.0, "Scales the chance for legendary cantrips to drop, relative to other cantrip levels in the tier. Defaults to 1.0, as per end of retail")),

                ("advocate_fane_auto_bestow_level", new Property<double>(1, "the level that advocates are automatically bestowed by Advocate Fane if advocate_fane_auto_bestow is true")),
                ("aetheria_drop_rate", new Property<double>(1.0, "Modifier for Aetheria drop rate, 1 being normal")),
                ("chess_ai_start_time", new Property<double>(-1.0, "the number of seconds for the chess ai to start. defaults to -1 (disabled)")),
                ("encounter_delay", new Property<double>(1800, "the number of seconds a generator profile for regions is delayed from returning to free slots")),
                ("encounter_regen_interval", new Property<double>(600, "the number of seconds a generator for regions at which spawns its next set of objects")),
                ("fast_missile_modifier", new Property<double>(1.2, "The speed multiplier applied to fast missiles. Defaults to retail value of 1.2")),
                ("ignore_magic_armor_pvp_scalar", new Property<double>(1.0, "Scales the effectiveness of IgnoreMagicArmor (ie. hollow weapons) in pvp battles. 1.0 = full effectiveness / ignore all enchantments on armor (default), 0.5 = half effectiveness / use half enchantments from armor, 0.0 = no effectiveness / use full enchantments from armor")),
                ("ignore_magic_resist_pvp_scalar", new Property<double>(1.0, "Scales the effectiveness of IgnoreMagicResist (ie. hollow weapons) in pvp battles. 1.0 = full effectiveness / ignore all resistances from life enchantments (default), 0.5 = half effectiveness / use half resistances from life enchantments, 0.0 = no effectiveness / use full resistances from life enchantments")),
                ("luminance_modifier", new Property<double>(1.0, "Scales the amount of luminance received by players")),
                ("melee_max_angle", new Property<double>(0.0, "for melee players, the maximum angle before a TurnTo is required. retail appeared to have required a TurnTo even for the smallest of angle offsets.")),
                ("mob_awareness_range", new Property<double>(1.0, "Scales the distance the monsters become alerted and aggro the players")),
                ("pk_new_character_grace_period", new Property<double>(300, "the number of seconds, in addition to pk_respite_timer, that a player killer is set to non-player killer status after first exiting training academy")),
                ("pk_respite_timer", new Property<double>(300, "the number of seconds that a player killer is set to non-player killer status after dying to another player killer")),
                ("quest_lum_modifier", new Property<double>(1.0, "Scale multiplier for amount of quest luminance received by players.  Quest lum is also modified by 'luminance_modifier'.")),
                ("quest_mindelta_rate", new Property<double>(1.0, "scales all quest min delta time between solves, 1 being normal")),
                ("quest_xp_modifier", new Property<double>(1.0, "Scale multiplier for amount of quest XP received by players.  Quest XP is also modified by 'xp_modifier'.")),   // Project ACX: retail 1.0 since 2026-11-17 (JJ: "Okay lets set it at retail"; was 0.81 with xp_modifier 1.4, quests ~13% over retail)
                ("rare_drop_rate_percent", new Property<double>(0.04, "Adjust the chance of a rare to spawn as a percentage. Default is 0.04, or 1 in 2,500. Max is 100, or every eligible drop.")),
                ("spellcast_max_angle", new Property<double>(20.0, "for advanced player spell casting, the maximum angle to target release a spell projectile. retail seemed to default to value of around 20, although some players seem to prefer a higher 45 degree angle")),
                ("trophy_drop_rate", new Property<double>(1.0, "Modifier for trophies dropped on creature death")),
                ("unlocker_window", new Property<double>(10.0, "The number of seconds a player unlocking a chest has exclusive access to first opening the chest.")),
                ("vendor_unique_rot_time", new Property<double>(300, "the number of seconds before unique items sold to vendors disappear")),
                ("vitae_penalty", new Property<double>(0.05, "the amount of vitae penalty a player gets per death")),
                ("vitae_penalty_max", new Property<double>(0.40, "the maximum vitae penalty a player can have")),
                ("void_pvp_modifier", new Property<double>(0.5, "Scales the amount of damage players take from Void Magic. Defaults to 0.5, as per retail. For earlier content where DRR isn't as readily available, this can be adjusted for balance.")),
                ("xp_modifier", new Property<double>(1.0, "scales the amount of xp received by players"))   // Project ACX: retail 1.0 since 2026-11-17 (JJ: "I think we should do the same for xp then increase later", "lets set it at retail"; was 1.4 from 2026-11-13: level 126 in ~6 months at 2 h a night, 5 nights a week; retail rates plus T.A.R. ~400 h). Raise it from the XP log (Logs/acx-xph.csv)
                );

        public static readonly ReadOnlyDictionary<string, Property<string>> DefaultStringProperties =
            DictOf(
                ("content_folder", new Property<string>("Content", "for content creators to live edit weenies. defaults to Content folder found in same directory as ACE.Server.dll")),
                ("dat_older_warning_msg", new Property<string>("Your DAT files are incomplete.\nThis server does not support dynamic DAT updating at this time.\nPlease visit https://emulator.ac/how-to-play to download the complete DAT files.", "Warning message displayed (if show_dat_warning is true) to player if client attempts DAT download from server")),
                ("dat_newer_warning_msg", new Property<string>("Your DAT files are newer than expected.\nPlease visit https://emulator.ac/how-to-play to download the correct DAT files.", "Warning message displayed (if show_dat_warning is true) to player if client connects to this server")),
                ("popup_header", new Property<string>("Welcome to Asheron's Call!", "Welcome message displayed when you log in")),
                ("popup_welcome", new Property<string>("Read the ACX Guide in your pack: it explains what's new on this server.\n\nTo begin your training, speak to the Society Greeter. Walk up to the Society Greeter using the 'W' key, then double-click on her to initiate a conversation.", "Welcome message popup in training halls (Project ACX: points to the ACX Guide, Player_AcxGuide.cs)")),
                ("popup_welcome_olthoi", new Property<string>("Welcome to the Olthoi hive! Be sure to talk to the Olthoi Queen to receive the Olthoi protections granted by the energies of the hive.", "Welcome message displayed on the first login for an Olthoi Player")),
                ("popup_motd", new Property<string>("", "Popup message of the day")),
                ("server_motd", new Property<string>("", "Server message of the day")),
                ("server_motd2", new Property<string>("", "Project ACX: second line of the message of the day")),
                ("server_motd3", new Property<string>("", "Project ACX: third line of the message of the day")),
                ("server_motd4", new Property<string>("", "Project ACX: fourth line of the message of the day")),
                ("acx_economy_log_started", new Property<string>("", "Project ACX: the day (UTC, yyyy-MM-dd) the economy log wrote its first line; it stops acx_economy_log_days later. Set by the server; clear it to start a new month")),
                ("acx_alchemist_low_tier_rule", new Property<string>("none", "(none = every type from tier 1, each tier its own grade: the default since the 2026-11-16 re-bench with real ammunition, gear and XP showed neither rule is needed.) Project ACX: how Alchemist bombs drop below loot tier 5 (AcxBombWcids; JJ 2026-11-16 benching both): t3grade = every type from tier 1, a tier 4 bomb's damage, Potency and debuff roll as tier 3's, the rest as tier 4's (JJ: \"why dont we skip tier 4 for the bombs and tier 3 is used all the way up to 5?\", \"we could bump the defense modifiers and spells on tier 4 but not touch damage\"); physical = only Razor / Shrapnel / Concussion through tier 4, the rest mixed in at tier 5 as atlatls are, all from tier 6"))
                );
    }
}
