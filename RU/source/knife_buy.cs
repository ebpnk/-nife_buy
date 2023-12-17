using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;



namespace KnifeShopPlugin
{
    public class KnifeShop : BasePlugin
    {
        private const string PluginName = "Knife buy";
        private const string PluginAuthor = "DoctorishHD";
        private const string PluginVersion = "1.0";
        private const string ConfigFileName = "knifeshop_config.json";
        private const string KnifePurchasesFileName = "knife_purchases.json";
        private KnifeShopConfig? config;
        private Dictionary<ulong, (string KnifeCommand, Timer KnifeTimer)> playerKnives = new Dictionary<ulong, (string, Timer)>();
        

        public override string ModuleName => PluginName;
        public override string ModuleVersion => PluginVersion;

        public override void Load(bool hotReload)
        {
            base.Load(hotReload);
            LoadConfig();
            AddCommand("buykf", "Купить нож", CommandBuyKnife);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
        }

        private void LoadConfig()
        {
            string configFilePath = Path.Combine(ModuleDirectory, ConfigFileName);
            if (!File.Exists(configFilePath))
            {
                // Создаем новый конфиг с примерными данными
                var defaultConfig = new KnifeShopConfig
                {
                    KnifePrices = new Dictionary<string, KnifeConfig>
                    {
                       {
                            "weapon_knife_karambit",
                            new KnifeConfig { Name = "Karambit", Price = 900, Duration = 60 }
                        },
                        {
                            "weapon_knife_butterfly",
                            new KnifeConfig { Name = "Butterfly Knife", Price = 800, Duration = 60 }
                        },
                        {
                            "weapon_knife_kukri",
                            new KnifeConfig { Name = "Kukri", Price = 700, Duration = 60 }
                        },
                        {
                            "weapon_knife_css",
                            new KnifeConfig { Name = "Classic knife", Price = 700, Duration = 60 }
                        },
                        {
                            "weapon_bayonet",
                            new KnifeConfig { Name = "Bayonet", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_flip",
                            new KnifeConfig { Name = "Flip Knife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_gut",
                            new KnifeConfig { Name = "Gutknife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_m9_bayonet",
                            new KnifeConfig { Name = "M9 Bayonet", Price = 800, Duration = 60 }
                        },
                        {
                            "weapon_knife_tactical",
                            new KnifeConfig { Name = "Huntsman Knife", Price = 800, Duration = 60 }
                        },
                        {
                            "weapon_knife_push",
                            new KnifeConfig { Name = "Shadow Daggers", Price = 800, Duration = 60 }
                        },
                        {
                            "weapon_knife_falchion",
                            new KnifeConfig { Name = "Falchion Knife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_survival_bowie",
                            new KnifeConfig { Name = "Bowie Knife", Price = 800, Duration = 60 }
                        },
                        {
                            "weapon_knife_ursus",
                            new KnifeConfig { Name = "Ursus Knife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_gypsy_jackknife",
                            new KnifeConfig { Name = "Navaja Knife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_stiletto",
                            new KnifeConfig { Name = "Stiletto Knife", Price = 750, Duration = 60 }
                        },
                        {
                            "weapon_knife_widowmaker",
                            new KnifeConfig { Name = "Talon Knife", Price = 750, Duration = 60 }
                        } 
                    }
                };

                string jsonConfig = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFilePath, jsonConfig);
                Console.WriteLine("Configuration file created with default settings.");
            }

            config = JsonSerializer.Deserialize<KnifeShopConfig>(File.ReadAllText(configFilePath));
        }

        private void CommandBuyKnife(CCSPlayerController player, CommandInfo commandInfo)
        {
            if (player == null || config == null)
            {
                return;
            }

            var knifeMenu = new ChatMenu("Выберите нож");
            foreach (var knife in config.KnifePrices)
            {
                string knifeName = knife.Value.Name;
                int price = knife.Value.Price;
                knifeMenu.AddMenuOption($"{knifeName} - {price}$", (p, o) => AttemptToPurchaseKnife(p, knife.Key, knife.Value));
            }

            ChatMenus.OpenMenu(player, knifeMenu);
        }

        private void AttemptToPurchaseKnife(CCSPlayerController player, string knifeCommand, KnifeConfig knifeConfig)
        {
            if (player.InGameMoneyServices.Account >= knifeConfig.Price)
            {
                player.InGameMoneyServices.Account -= knifeConfig.Price;
                GiveKnife(player, knifeCommand);
                player.PrintToChat($"Вы купили {knifeConfig.Name} за {knifeConfig.Price}$, который будет доступен в течение {knifeConfig.Duration} секунд.");

                // Сохранение информации о покупке в конфигурационный файл
                WriteKnifePurchaseToConfig(player.SteamID, knifeCommand, knifeConfig.Duration);

                // Установка таймера для удаления записи о покупке из конфига
                SetTimerToRemoveKnife(player.SteamID, knifeConfig.Duration);
            }
            else
            {
                player.PrintToChat("У вас недостаточно денег для покупки этого ножа.");
            }
        }

        private void SetTimerToRemoveKnife(ulong steamId, int duration)
        {
            new Timer(duration, () =>
            {
                RemoveKnifePurchaseFromConfig(steamId);
                var player = Utilities.GetPlayerFromSteamId(steamId);
                if (player != null)
                {
                    player.PrintToChat("Время действия вашего ножа истекло.");
                }
            });
        }

        private void GiveKnife(CCSPlayerController player, string knifeCommand)
        {
            // Та же логика, что и ранее
            RemoveCurrentKnife(player); 
            player.GiveNamedItem(knifeCommand);
        }

        private void RemoveKnife(CCSPlayerController player, string knifeCommand)
        {
            if (playerKnives.TryGetValue(player.SteamID, out var knifeData) && knifeData.KnifeCommand == knifeCommand)
            {
                RemoveCurrentKnife(player);
                playerKnives.Remove(player.SteamID);
            }
        }

        private void RemoveCurrentKnife(CCSPlayerController player)
        {
            var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;
            foreach (var weapon in weapons)
            {
                if (weapon != null && weapon.IsValid && IsKnife(weapon.Value.DesignerName))
                {
                    weapon.Value.Remove(); // Удаляем нож
                    break;
                }
            }
        }

        private static bool IsKnife(string weaponName)
        {
            return !string.IsNullOrEmpty(weaponName) && 
                (weaponName.Contains("knife") || weaponName.Contains("bayonet"));
        }

        [GameEventHandler]
        private HookResult OnRoundStart(EventRoundStart roundStartEvent, GameEventInfo info)
        {
            var knifePurchases = ReadKnifePurchasesFromConfig();
            foreach (var purchase in knifePurchases)
            {
                var player = Utilities.GetPlayerFromSteamId(purchase.Key);
                if (player != null && player.IsValid)
                {
                    GiveKnife(player, purchase.Value.KnifeCommand);
                }
            }
            return HookResult.Continue;
        }

        // Определение метода GetPlayer
        private CCSPlayerController GetPlayer(ulong userId)
        {
            return Utilities.GetPlayerFromUserid((int)userId);
        }

        private void WriteKnifePurchaseToConfig(ulong steamId, string knifeCommand, int duration)
        {
            var filePath = Path.Combine(ModuleDirectory, KnifePurchasesFileName);
            var knifePurchases = File.Exists(filePath) 
                ? JsonSerializer.Deserialize<Dictionary<ulong, KnifePurchase>>(File.ReadAllText(filePath)) 
                ?? new Dictionary<ulong, KnifePurchase>() 
                : new Dictionary<ulong, KnifePurchase>();

            knifePurchases[steamId] = new KnifePurchase { KnifeCommand = knifeCommand, Duration = duration };

            string json = JsonSerializer.Serialize(knifePurchases, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        private void RemoveKnifePurchaseFromConfig(ulong steamId)
        {
            var filePath = Path.Combine(ModuleDirectory, KnifePurchasesFileName);
            var knifePurchases = File.Exists(filePath) 
                ? JsonSerializer.Deserialize<Dictionary<ulong, KnifePurchase>>(File.ReadAllText(filePath)) 
                ?? new Dictionary<ulong, KnifePurchase>() 
                : new Dictionary<ulong, KnifePurchase>();

            if (knifePurchases.ContainsKey(steamId))
            {
                knifePurchases.Remove(steamId);

                string json = JsonSerializer.Serialize(knifePurchases, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
        }

        private Dictionary<ulong, KnifePurchase> ReadKnifePurchasesFromConfig()
        {
            var filePath = Path.Combine(ModuleDirectory, KnifePurchasesFileName);
            if (!File.Exists(filePath))
            {
                return new Dictionary<ulong, KnifePurchase>();
            }

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<ulong, KnifePurchase>>(json) ?? new Dictionary<ulong, KnifePurchase>();
        }

    }

    public class KnifeShopConfig
    {
        public Dictionary<string, KnifeConfig> KnifePrices { get; set; } = new Dictionary<string, KnifeConfig>();
    }

    public class KnifeConfig
    {
        public string Name { get; set; }
        public int Price { get; set; }
        public int Duration { get; set; }
    }

    public class KnifePurchase
    {
        public string KnifeCommand { get; set; }
        public int Duration { get; set; }
    }
}
