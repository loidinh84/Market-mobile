using Godot;

namespace MarketMobile.Core;

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

/// <summary>
/// Danh mục vật phẩm và trang phục dùng chung giữa Cửa hàng và Túi đồ.
/// </summary>
public static class GameCatalog
{
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
}
