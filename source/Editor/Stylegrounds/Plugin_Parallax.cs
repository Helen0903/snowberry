﻿using Celeste;

namespace Snowberry.Editor.Stylegrounds {

	[Plugin("parallax")]
	internal class Plugin_Parallax : Styleground {

		[Option("texture")] public string Texture = "";
		[Option("atlas")] public string Atlas = "game";
		[Option("blendmode")] public string BlendMode = "alphablend";
		[Option("fadeIn")] public bool FadeIn = false;

		public override void Render() {
			base.Render();
		}
	}
}
