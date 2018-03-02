#include "RgbHsv.h"

double Min(double a, double b) {
	return a <= b ? a : b;
}

double Max(double a, double b) {
	return a >= b ? a : b;
}

HSV RGBToHSV(RGB rgb) {
	double delta, min;
	double h = 0, s, v;

	min = Min(Min(rgb.R, rgb.G), rgb.B);
	v = Max(Max(rgb.R, rgb.G), rgb.B);
	delta = v - min;

	if (v == 0.0)
		s = 0;
	else
		s = delta / v;

	if (s == 0)
		h = 0.0;

	else
	{
		if (rgb.R == v)
			h = (rgb.G - rgb.B) / delta;
		else if (rgb.G == v)
			h = 2 + (rgb.B - rgb.R) / delta;
		else if (rgb.B == v)
			h = 4 + (rgb.R - rgb.G) / delta;

		h *= 60;

		if (h < 0.0)
			h = h + 360;
	}

	return HSV(h, s * 100, (v / 255) * 100);
}