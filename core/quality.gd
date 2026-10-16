class_name Quality
## 品质：绿/蓝/紫/金（02 文档 §4.2 边框色）。

const Order := ["green", "blue", "purple", "gold"]


static func cn(q: String) -> String:
	match q:
		"green":
			return "绿"
		"blue":
			return "蓝"
		"purple":
			return "紫"
		"gold":
			return "金"
	return "?"


static func tier(q: String) -> int:
	return maxi(0, Order.find(q))


static func color(q: String) -> Color:
	match q:
		"green":
			return Color("#4CAF6A")
		"blue":
			return Color("#3E86C6")
		"purple":
			return Color("#8A5BC6")
		"gold":
			return Color("#E0A32E")
	return Color("#8B8B85")
