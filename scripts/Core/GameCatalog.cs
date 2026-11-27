using Godot;

namespace MarketMobile.Core;

public enum PlayMatchType
{
	Normal = 0,
	Ranked = 1,
	Bot = 2,
	Custom = 3
}

public enum TeamMode
{
	Solo1v1 = 0,
	Duo2v2 = 1
}

public enum BotDifficulty
{
	Easy = 0,
	Medium = 1,
	Hard = 2
}

public sealed class MapMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Subtitle { get; init; }
	public string TexturePath { get; init; }
	public Color BannerTint { get; init; } = Colors.White;
	public Color TitleColor { get; init; } = Colors.White;
	public bool AllowInRanked { get; init; } = true;
	public bool IsComingSoon { get; init; } = false;
}

public sealed class ConsumableItemMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Tag { get; init; }
	public string Summary { get; init; }
	public string Description { get; init; }
	public int Price { get; init; }
	public CurrencyType Currency { get; init; }
	public string IconPath { get; init; }
	public Color IconTint { get; init; } = Colors.White;
	public bool IsConsumable { get; init; } = false;
}

public sealed class SkinItemMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Tag { get; init; }
	public string Summary { get; init; }
	public string Description { get; init; }
	public int Price { get; init; }
	public CurrencyType Currency { get; init; }
	public string IconPath { get; init; }
	public Color IconTint { get; init; } = Colors.White;
}

public sealed class GiftBagMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Tag { get; init; }
	public string Summary { get; init; }
	public string Description { get; init; }
	public string IconPath { get; init; }
	public Color IconTint { get; init; } = Colors.White;
}

public sealed class FriendMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public bool IsOnline { get; init; } = true;
	public bool IsInMatch { get; init; } = false;
}

public sealed class StickerMeta
{
	public string Id { get; init; }
	public string PackId { get; init; }
	public string Label { get; init; }
	public string TexturePath { get; init; }
}

public sealed class StickerPackMeta
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string CoverPath { get; init; }
	public StickerMeta[] Items { get; init; }
}

/// <summary>
/// Danh mục vật phẩm và trang phục dùng chung giữa Cửa hàng và Túi đồ.
/// </summary>
public static class GameCatalog
{
	public const float RedRadishIntervalSeconds = 3.0f;
	public const float WhiteRadishIntervalSeconds = 5.0f;
	public const int MaxCustomSpectators = 4;
	public static readonly int[] AllowedSpectatorPresets = { 0, 2, 4 };

	public static int NormalizeSpectatorCount(int raw)
	{
		if (raw <= 0) return 0;
		if (raw <= 2) return 2;
		return MaxCustomSpectators;
	}

	public static readonly FriendMeta[] SampleFriends =
	{
		new() { Id = "100001", Name = "Nguyễn Trọng Nhân", IsOnline = true },
		new() { Id = "100002", Name = "Trần Minh Khoa", IsOnline = true },
		new() { Id = "100003", Name = "Lê Hoàng Nam", IsOnline = true },
		new() { Id = "100004", Name = "Phạm Gia Bảo", IsOnline = true },
		new() { Id = "100005", Name = "Võ Thanh Tùng", IsOnline = true, IsInMatch = true },
		new() { Id = "100006", Name = "Nguyễn Thị Mai", IsOnline = true },
		new() { Id = "100007", Name = "Đặng Quốc Huy", IsOnline = true },
		new() { Id = "100008", Name = "Bùi Ngọc Anh", IsOnline = true },
		new() { Id = "100009", Name = "Huỳnh Tấn Phát", IsOnline = true, IsInMatch = true },
		new() { Id = "100010", Name = "Đỗ Khánh Linh", IsOnline = true },
		new() { Id = "100011", Name = "Phan Văn Đức", IsOnline = true },
		new() { Id = "100012", Name = "Ngô Thùy Dương", IsOnline = false },
		new() { Id = "100013", Name = "Lý Minh Tuấn", IsOnline = false },
		new() { Id = "100014", Name = "Trương Hoàng Phúc", IsOnline = true },
		new() { Id = "100015", Name = "Cao Thảo Vy", IsOnline = false }
	};

	public static readonly FriendMeta[] SearchablePlayers =
	{
		new() { Id = "200101", Name = "Thành Lợi", IsOnline = true },
		new() { Id = "200102", Name = "Anh Tèo", IsOnline = true },
		new() { Id = "200103", Name = "Chị Hằng", IsOnline = true },
		new() { Id = "200104", Name = "Chú Cuội", IsOnline = true },
		new() { Id = "200105", Name = "Hai Lúa Miền Tây", IsOnline = true },
		new() { Id = "200106", Name = "Sát Thủ Rau Củ", IsOnline = false },
		new() { Id = "200107", Name = "Vua Chợ Đêm", IsOnline = true },
		new() { Id = "200108", Name = "Bà Năm Bán Cá", IsOnline = false },
		new() { Id = "200109", Name = "Cô Ba Sài Gòn", IsOnline = true },
		new() { Id = "200110", Name = "Đại Gia Hạt Dẻ", IsOnline = true }
	};

	public static readonly FriendMeta[] SampleFriendRequests =
	{
		new() { Id = "300101", Name = "Nguyễn Minh Anh", IsOnline = true },
		new() { Id = "300102", Name = "Trần Quốc Bảo", IsOnline = true },
		new() { Id = "300103", Name = "Lê Thu Hà", IsOnline = false },
		new() { Id = "300104", Name = "Phạm Tuấn Kiệt", IsOnline = true },
		new() { Id = "300105", Name = "Hoàng Gia Hân", IsOnline = true },
		new() { Id = "300106", Name = "Vũ Đức Mạnh", IsOnline = false }
	};

	public static readonly ConsumableItemMeta[] Consumables =
	{
		new()
		{
			Id = "item_tra_da", Name = "Trà Đá Hồi Sức", Tag = "Tiêu hao",
			Summary = "Hồi +40% Máu tức thì",
			Description = "Ly trà đá mát lạnh giúp hồi nhanh 40% lượng máu khi giao tranh ở chợ.",
			Price = 150, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/emotion.png", IconTint = new Color(0.55f, 0.95f, 1.0f, 1f),
			IsConsumable = true
		},
		new()
		{
			Id = "item_gio_hang", Name = "Giỏ Hàng Siêu Tốc", Tag = "Bổ trợ",
			Summary = "+25% Tốc chạy trong 15s",
			Description = "Đẩy xe hàng nhẹ như bay, tăng tốc độ di chuyển và né đòn trong 15 giây.",
			Price = 250, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/tab_shop.png", IconTint = new Color(1.0f, 0.92f, 0.65f, 1f),
			IsConsumable = false
		},
		new()
		{
			Id = "item_nuoc_mia", Name = "Nước Mía Tăng Lực", Tag = "Tấn công",
			Summary = "+20% Sát thương trong 20s",
			Description = "Nạp năng lượng tức thì, giúp đòn đánh mạnh hơn 20% trong 20 giây.",
			Price = 200, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/upgrade.png", IconTint = new Color(1.0f, 0.85f, 0.35f, 1f),
			IsConsumable = true
		},
		new()
		{
			Id = "item_bua_x2", Name = "Bùa X2 Doanh Thu", Tag = "Kinh tế",
			Summary = "Nhân đôi Vàng thưởng trận",
			Description = "Kích hoạt trước ván đấu để tăng 100% số Vàng nhận được khi thắng.",
			Price = 80, Currency = CurrencyType.Rau,
			IconPath = "res://assets/sprites/icons/coin.png", IconTint = Colors.White,
			IsConsumable = true
		},
		new()
		{
			Id = "item_hat_giong", Name = "Túi Hạt Giống Quý", Tag = "Thu hoạch",
			Summary = "Thêm +30 Rau sau màn",
			Description = "Gieo trồng sau giờ chợ, nhận thưởng thêm 30 Rau sạch cuối mỗi trận.",
			Price = 400, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/rau.png", IconTint = Colors.White,
			IsConsumable = true
		},
		new()
		{
			Id = "item_giap_noi", Name = "Khiên Nắp Vung", Tag = "Phòng thủ",
			Summary = "Giảm 25% sát thương nhận",
			Description = "Nắp vung inox bền bỉ giúp giảm 25% sát thương phải chịu trong 15 giây.",
			Price = 120, Currency = CurrencyType.Rau,
			IconPath = "res://assets/sprites/icons/role.png", IconTint = new Color(0.70f, 0.90f, 1.0f, 1f),
			IsConsumable = false
		}
	};

	public static readonly SkinItemMeta[] Skins =
	{
		new()
		{
			Id = "outfit_default", Name = "Áo Dài Đỏ", Tag = "Mặc định",
			Summary = "Trang phục gốc Bà Bảy",
			Description = "Bộ áo dài truyền thống đã mở sẵn và đồng bộ với tab Nhân vật.",
			Price = 0, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/ao_dai.png", IconTint = Colors.White
		},
		new()
		{
			Id = "hat_pink", Name = "Nón Hoa Hồng", Tag = "Phụ kiện",
			Summary = "Đồng bộ tab Nhân vật",
			Description = "Chiếc nón thanh lịch dành riêng cho Bà Bảy. Mua 1 lần dùng chung ở cả hai tab.",
			Price = 1500, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/hat_pink.png", IconTint = Colors.White
		},
		new()
		{
			Id = "skin_apron_gold", Name = "Áo Dài Hoàng Kim", Tag = "Hiếm",
			Summary = "+5% Vàng rơi thêm",
			Description = "Phiên bản áo dài sắc vàng rực rỡ dành cho tiểu thương nổi bật nhất chợ.",
			Price = 350, Currency = CurrencyType.Rau,
			IconPath = "res://assets/sprites/icons/ao_dai.png", IconTint = new Color(1.0f, 0.88f, 0.42f, 1f)
		},
		new()
		{
			Id = "skin_hat_jade", Name = "Nón Ngọc Bích", Tag = "Độc quyền",
			Summary = "Nổi bật khi đứng sảnh",
			Description = "Chiếc nón ánh ngọc bích mát mắt, mở khóa bằng Rau sạch.",
			Price = 450, Currency = CurrencyType.Rau,
			IconPath = "res://assets/sprites/icons/hat_pink.png", IconTint = new Color(0.55f, 1.0f, 0.85f, 1f)
		},
		new()
		{
			Id = "skin_night_violet", Name = "Áo Dài Chợ Đêm", Tag = "Đặc biệt",
			Summary = "Sắc tím đèn lồng",
			Description = "Trang phục tím huyền ảo lấy cảm hứng từ phiên chợ đêm lung linh.",
			Price = 2800, Currency = CurrencyType.Gold,
			IconPath = "res://assets/sprites/icons/ao_dai.png", IconTint = new Color(0.75f, 0.58f, 1.0f, 1f)
		}
	};

	public static readonly GiftBagMeta[] GiftBags =
	{
		new()
		{
			Id = "gift_tan_thu", Name = "Túi Quà Tân Thủ", Tag = "Túi quà",
			Summary = "+300 Vàng · +50 Rau · x2 Trà Đá",
			Description = "Phần quà chào mừng tiểu thương mới ra chợ. Mở ngay để nhận Vàng, Rau sạch và Trà Đá Hồi Sức!",
			IconPath = "res://assets/sprites/icons/animation_chest/chest_full_00.png", IconTint = Colors.White
		},
		new()
		{
			Id = "gift_loc_cho", Name = "Túi Lộc Phiên Chợ", Tag = "Quà may mắn",
			Summary = "Ngẫu nhiên Vàng, Rau & Tiêu thụ",
			Description = "Mở ra để nhận ngẫu nhiên Vàng, Rau sạch hoặc vật phẩm tiêu thụ hỗ trợ giao tranh.",
			IconPath = "res://assets/sprites/icons/coin_poster.png", IconTint = Colors.White
		}
	};

	public static readonly MapMeta[] Maps =
	{
		new()
		{
			Id = "map_cho_dau_moi",
			Name = "Chợ Đầu Mối",
			Subtitle = "Bản đồ chuẩn · Nhịp độ cân bằng",
			TexturePath = "res://assets/sprites/icons/map_demo.png",
			BannerTint = Colors.White,
			TitleColor = new Color(1.0f, 0.90f, 0.25f, 1f),
			AllowInRanked = true
		},
		new()
		{
			Id = "map_nga_tu_thu_duc",
			Name = "Ngã Tư Thủ Đức",
			Subtitle = "Chỉ mở Đấu Thường / Máy / Custom",
			TexturePath = "res://assets/sprites/icons/map_demo.png",
			BannerTint = new Color(0.95f, 0.92f, 1.0f, 1f),
			TitleColor = new Color(1.0f, 0.62f, 0.22f, 1f),
			AllowInRanked = false
		},
		new()
		{
			Id = "map_cho_ben_thanh",
			Name = "Chợ Bến Thành",
			Subtitle = "Bản đồ chuẩn · Giao tranh dồn dập",
			TexturePath = "res://assets/sprites/icons/map_demo.png",
			BannerTint = new Color(1.0f, 0.93f, 0.90f, 1f),
			TitleColor = new Color(1.0f, 0.36f, 0.32f, 1f),
			AllowInRanked = true
		},
		new()
		{
			Id = "map_sap_ra_mat",
			Name = "Sắp Ra Mắt",
			Subtitle = "Đang xây dựng thêm khu chợ mới",
			TexturePath = "res://assets/sprites/icons/map_demo.png",
			BannerTint = new Color(0.75f, 0.75f, 0.82f, 1f),
			TitleColor = new Color(0.95f, 0.95f, 1.0f, 1f),
			AllowInRanked = false,
			IsComingSoon = true
		}
	};

	public static MapMeta FindMap(string mapId)
	{
		if (!string.IsNullOrEmpty(mapId))
		{
			foreach (var m in Maps)
			{
				if (m.Id == mapId) return m;
			}
		}
		return Maps[0];
	}

	public static string GetPlayTypeShortLabel(PlayMatchType type) => type switch
	{
		PlayMatchType.Ranked => "Xếp hạng",
		PlayMatchType.Bot => "Đấu máy",
		PlayMatchType.Custom => "Custom",
		_ => "Thường"
	};

	public static string GetTeamModeLabel(TeamMode mode) => mode == TeamMode.Duo2v2 ? "2 VS 2" : "1 VS 1";

	public static string GetBotDifficultyLabel(BotDifficulty diff) => diff switch
	{
		BotDifficulty.Hard => "Khó",
		BotDifficulty.Medium => "Vừa",
		_ => "Dễ"
	};

	public static ConsumableItemMeta FindConsumable(string itemId)
	{
		if (string.IsNullOrEmpty(itemId)) return null;
		foreach (var item in Consumables)
		{
			if (item.Id == itemId) return item;
		}
		return null;
	}

	public static GiftBagMeta FindGiftBag(string giftId)
	{
		if (string.IsNullOrEmpty(giftId)) return null;
		foreach (var bag in GiftBags)
		{
			if (bag.Id == giftId) return bag;
		}
		return null;
	}

	private static StickerMeta[] BuildStickerPackItems(string packId, string folder, params string[] labels)
	{
		var items = new StickerMeta[labels.Length];
		for (int i = 0; i < labels.Length; i++)
		{
			string code = $"{packId}_{(i + 1):D2}";
			items[i] = new StickerMeta
			{
				Id = code,
				PackId = packId,
				Label = labels[i],
				TexturePath = $"res://assets/sprites/stickers/packs/{folder}/{code}.png"
			};
		}
		return items;
	}

	public static readonly StickerPackMeta[] StickerPacks =
	{
		new()
		{
			Id = "capybara",
			Name = "Capybara",
			CoverPath = "res://assets/sprites/stickers/packs/capybara/capybara_01.png",
			Items = BuildStickerPackItems(
				"capybara", "capybara",
				"Thả tim", "Sinh nhật", "Đánh đàn", "Vui sướng",
				"Chào nha", "Kính hồng", "Siêu ngầu", "Trà sữa",
				"Bánh quy", "Ăn kem", "Ôm dâu", "Burger",
				"Ngủ ngon", "Bánh kem", "Cõng ếch", "Cười tít",
				"Mũ ếch", "Bánh donut", "Dưa hấu", "Đeo kính",
				"Làm việc", "Đọc sách", "Xếp sách", "Anh em"
			)
		},
		new()
		{
			Id = "duck",
			Name = "Vịt Vàng",
			CoverPath = "res://assets/sprites/stickers/packs/duck/duck_01.png",
			Items = BuildStickerPackItems(
				"duck", "duck",
				"Ôm tim", "Mũ ếch", "Xin chào", "Mũ hoa cúc",
				"Hoa tulip", "Ngủ khò", "Kính tim", "Trà sữa",
				"Áo mưa", "Vương miện", "Quàng khăn", "Ôm gấu",
				"Nở trứng", "Mũ cói", "Hoa cúc", "Thư giãn",
				"Phao bơi", "Hoan hô", "Đọc sách", "Nơ hồng"
			)
		},
		new()
		{
			Id = "pochacco",
			Name = "Pochacco",
			CoverPath = "res://assets/sprites/stickers/packs/pochacco/pochacco_01.png",
			Items = BuildStickerPackItems(
				"pochacco", "pochacco",
				"Ngơ ngác", "Dỏng tai", "Mơ màng",
				"Tức giận", "Tặng tim", "Toát mồ hôi"
			)
		}
	};

	public static readonly string[] QuickChatPhrases =
	{
		"Vô kèo 2v2 không?",
		"Lập phòng nhé!",
		"Đợi mình xíu nha!",
		"Sẵn sàng rồi nè!",
		"Hay quá bạn ơi!",
		"Cố lên nào!",
		"Thiếu 1 slot nữa!",
		"Trận hay, cảm ơn nha!"
	};

	public static StickerMeta FindSticker(string stickerId)
	{
		if (string.IsNullOrEmpty(stickerId)) return null;
		foreach (var pack in StickerPacks)
		{
			foreach (var item in pack.Items)
			{
				if (item.Id == stickerId) return item;
			}
		}
		return null;
	}
}
