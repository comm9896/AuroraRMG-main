namespace OldenEraTemplateEditor.Models
{
    public class ContentPoolInfo
    {
        public string Sid { get; set; } = "";
        public string Category { get; set; } = "";
        public string Tier { get; set; } = "";
        public string ContentType { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public static class ContentPoolData
    {
        public static readonly List<ContentPoolInfo> Pools = new()
        {
            // ── Guarded pools ──
            new() { Sid = "classic_template_pool_random_t2_item", Category = "Guarded", Tier = "T2", ContentType = "item", Description = "Случайный предмет (T2)" },
            new() { Sid = "classic_template_pool_random_t2_pandora", Category = "Guarded", Tier = "T2", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T2)" },
            new() { Sid = "classic_template_pool_random_t2_hire", Category = "Guarded", Tier = "T2", ContentType = "hire", Description = "Случайный наёмник (T2)" },
            new() { Sid = "classic_template_pool_random_t2_unit_bank", Category = "Guarded", Tier = "T2", ContentType = "unit_bank", Description = "Банк существ (T2)" },
            new() { Sid = "classic_template_pool_random_t2_res_bank", Category = "Guarded", Tier = "T2", ContentType = "res_bank", Description = "Банк ресурсов (T2)" },
            new() { Sid = "classic_template_pool_random_t2_stat", Category = "Guarded", Tier = "T2", ContentType = "stat", Description = "Здание характеристик героя (T2)" },
            new() { Sid = "classic_template_pool_random_t2_magic", Category = "Guarded", Tier = "T2", ContentType = "magic", Description = "Магическое здание (T2)" },

            new() { Sid = "classic_template_pool_random_t3_item", Category = "Guarded", Tier = "T3", ContentType = "item", Description = "Случайный предмет (T3)" },
            new() { Sid = "classic_template_pool_random_t3_pandora", Category = "Guarded", Tier = "T3", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T3)" },
            new() { Sid = "classic_template_pool_random_t3_hire", Category = "Guarded", Tier = "T3", ContentType = "hire", Description = "Случайный наёмник (T3)" },
            new() { Sid = "classic_template_pool_random_t3_unit_bank", Category = "Guarded", Tier = "T3", ContentType = "unit_bank", Description = "Банк существ (T3)" },
            new() { Sid = "classic_template_pool_random_t3_res_bank", Category = "Guarded", Tier = "T3", ContentType = "res_bank", Description = "Банк ресурсов (T3)" },
            new() { Sid = "classic_template_pool_random_t3_stat", Category = "Guarded", Tier = "T3", ContentType = "stat", Description = "Здание характеристик героя (T3)" },
            new() { Sid = "classic_template_pool_random_t3_magic", Category = "Guarded", Tier = "T3", ContentType = "magic", Description = "Магическое здание (T3)" },

            new() { Sid = "classic_template_pool_random_t4_item", Category = "Guarded", Tier = "T4", ContentType = "item", Description = "Случайный предмет (T4)" },
            new() { Sid = "classic_template_pool_random_t4_pandora", Category = "Guarded", Tier = "T4", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T4)" },
            new() { Sid = "classic_template_pool_random_t4_hire", Category = "Guarded", Tier = "T4", ContentType = "hire", Description = "Случайный наёмник (T4)" },
            new() { Sid = "classic_template_pool_random_t4_unit_bank", Category = "Guarded", Tier = "T4", ContentType = "unit_bank", Description = "Банк существ (T4)" },
            new() { Sid = "classic_template_pool_random_t4_res_bank", Category = "Guarded", Tier = "T4", ContentType = "res_bank", Description = "Банк ресурсов (T4)" },
            new() { Sid = "classic_template_pool_random_t4_stat", Category = "Guarded", Tier = "T4", ContentType = "stat", Description = "Здание характеристик героя (T4)" },
            new() { Sid = "classic_template_pool_random_t4_magic", Category = "Guarded", Tier = "T4", ContentType = "magic", Description = "Магическое здание (T4)" },

            new() { Sid = "classic_template_pool_random_t5_item", Category = "Guarded", Tier = "T5", ContentType = "item", Description = "Случайный предмет (T5)" },
            new() { Sid = "classic_template_pool_random_t5_pandora", Category = "Guarded", Tier = "T5", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T5)" },
            new() { Sid = "classic_template_pool_random_t5_hire", Category = "Guarded", Tier = "T5", ContentType = "hire", Description = "Случайный наёмник (T5)" },
            new() { Sid = "classic_template_pool_random_t5_unit_bank", Category = "Guarded", Tier = "T5", ContentType = "unit_bank", Description = "Банк существ (T5)" },
            new() { Sid = "classic_template_pool_random_t5_res_bank", Category = "Guarded", Tier = "T5", ContentType = "res_bank", Description = "Банк ресурсов (T5)" },
            new() { Sid = "classic_template_pool_random_t5_stat", Category = "Guarded", Tier = "T5", ContentType = "stat", Description = "Здание характеристик героя (T5)" },
            new() { Sid = "classic_template_pool_random_t5_magic", Category = "Guarded", Tier = "T5", ContentType = "magic", Description = "Магическое здание (T5)" },

            // ── Unguarded pools ──
            new() { Sid = "classic_template_pool_random_unguarded_t2_item", Category = "Unguarded", Tier = "T2", ContentType = "item", Description = "Случайный предмет (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_pandora", Category = "Unguarded", Tier = "T2", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_hire", Category = "Unguarded", Tier = "T2", ContentType = "hire", Description = "Случайный наёмник (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_unit_bank", Category = "Unguarded", Tier = "T2", ContentType = "unit_bank", Description = "Банк существ (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_res_bank", Category = "Unguarded", Tier = "T2", ContentType = "res_bank", Description = "Банк ресурсов (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_stat", Category = "Unguarded", Tier = "T2", ContentType = "stat", Description = "Здание характеристик героя (T2)" },
            new() { Sid = "classic_template_pool_random_unguarded_t2_magic", Category = "Unguarded", Tier = "T2", ContentType = "magic", Description = "Магическое здание (T2)" },

            new() { Sid = "classic_template_pool_random_unguarded_t3_item", Category = "Unguarded", Tier = "T3", ContentType = "item", Description = "Случайный предмет (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_pandora", Category = "Unguarded", Tier = "T3", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_hire", Category = "Unguarded", Tier = "T3", ContentType = "hire", Description = "Случайный наёмник (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_unit_bank", Category = "Unguarded", Tier = "T3", ContentType = "unit_bank", Description = "Банк существ (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_res_bank", Category = "Unguarded", Tier = "T3", ContentType = "res_bank", Description = "Банк ресурсов (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_stat", Category = "Unguarded", Tier = "T3", ContentType = "stat", Description = "Здание характеристик героя (T3)" },
            new() { Sid = "classic_template_pool_random_unguarded_t3_magic", Category = "Unguarded", Tier = "T3", ContentType = "magic", Description = "Магическое здание (T3)" },

            new() { Sid = "classic_template_pool_random_unguarded_t4_item", Category = "Unguarded", Tier = "T4", ContentType = "item", Description = "Случайный предмет (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_pandora", Category = "Unguarded", Tier = "T4", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_hire", Category = "Unguarded", Tier = "T4", ContentType = "hire", Description = "Случайный наёмник (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_unit_bank", Category = "Unguarded", Tier = "T4", ContentType = "unit_bank", Description = "Банк существ (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_res_bank", Category = "Unguarded", Tier = "T4", ContentType = "res_bank", Description = "Банк ресурсов (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_stat", Category = "Unguarded", Tier = "T4", ContentType = "stat", Description = "Здание характеристик героя (T4)" },
            new() { Sid = "classic_template_pool_random_unguarded_t4_magic", Category = "Unguarded", Tier = "T4", ContentType = "magic", Description = "Магическое здание (T4)" },

            new() { Sid = "classic_template_pool_random_unguarded_t5_item", Category = "Unguarded", Tier = "T5", ContentType = "item", Description = "Случайный предмет (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_pandora", Category = "Unguarded", Tier = "T5", ContentType = "pandora", Description = "Случайная шкатулка Пандоры (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_hire", Category = "Unguarded", Tier = "T5", ContentType = "hire", Description = "Случайный наёмник (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_unit_bank", Category = "Unguarded", Tier = "T5", ContentType = "unit_bank", Description = "Банк существ (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_res_bank", Category = "Unguarded", Tier = "T5", ContentType = "res_bank", Description = "Банк ресурсов (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_stat", Category = "Unguarded", Tier = "T5", ContentType = "stat", Description = "Здание характеристик героя (T5)" },
            new() { Sid = "classic_template_pool_random_unguarded_t5_magic", Category = "Unguarded", Tier = "T5", ContentType = "magic", Description = "Магическое здание (T5)" },

            // ── Resources pools ──
            new() { Sid = "content_pool_general_resources_start_zone_poor", Category = "Resources", Tier = "Poor", ContentType = "resources", Description = "Бедные ресурсы стартовой зоны" },
            new() { Sid = "content_pool_general_resources_start_zone_medium", Category = "Resources", Tier = "Medium", ContentType = "resources", Description = "Средние ресурсы стартовой зоны" },
            new() { Sid = "content_pool_general_resources_start_zone_rich", Category = "Resources", Tier = "Rich", ContentType = "resources", Description = "Богатые ресурсы стартовой зоны" },
        };

        public static ContentPoolInfo? GetPool(string sid)
        {
            return Pools.FirstOrDefault(p => p.Sid == sid);
        }

        public static List<ContentPoolInfo> GetPoolsByCategory(string category)
        {
            return Pools.Where(p => p.Category == category).ToList();
        }

        public static List<string> GetSelectedPoolSids(List<string>? selectedSids)
        {
            if (selectedSids == null || selectedSids.Count == 0)
                return new List<string>();
            return selectedSids.Where(sid => Pools.Any(p => p.Sid == sid)).ToList();
        }
    }
}
