﻿using Celeste;

using Microsoft.Xna.Framework.Graphics.PackedVector;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Monocle;

namespace Snowberry.Editor.Stylegrounds {

	[Plugin("parallax")]
	internal class Plugin_Parallax : Styleground {

		[Option("texture")] public string Texture = "";
		[Option("atlas")] public string Atlas = "game";
		[Option("blendmode")] public string BlendMode = "alphablend";
		[Option("fadeIn")] public bool FadeIn = false;

		// image: "bgs/blah" in "game"
		public override string Title() => $"{Dialog.Clean("SNOWBERRY_STYLEGROUNDS_IMAGE")}: \"{Texture}\" {(Atlas != "game" ? $"in {Atlas}" : "")}";

		public override void Render() {
			base.Render();

			Editor editor = Editor.Instance;
			if (editor == null)
				return;
			
			MTexture mtex = (Atlas == "game" && GFX.Game.Has(Texture)) ? GFX.Game[Texture] : (Atlas == "gui" && GFX.Gui.Has(Texture) ? GFX.Gui[Texture] : GFX.Misc[Texture]);
			Vector2 cameraPos = editor.Camera.Position.Floor() - new Vector2(160, 90);
			Vector2 pos = (Position + cameraPos * (Vector2.One - Scroll / editor.Camera.Zoom)).Floor();

			if (Color.A <= 1)
				return;

			SpriteEffects flip = SpriteEffects.None;
			if (FlipX && FlipY) {
				flip = SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically;
			} else if (FlipX) {
				flip = SpriteEffects.FlipHorizontally;
			} else if (FlipY) {
				flip = SpriteEffects.FlipVertically;
			}

			if (LoopX) {
				while (pos.X < editor.Camera.ViewRect.X)
					pos.X += mtex.Width;
				while (pos.X > editor.Camera.ViewRect.X)
					pos.X -= mtex.Width;
			}
			
			if (LoopY) {
				while (pos.Y < editor.Camera.ViewRect.Y)
					pos.Y += mtex.Height;
				while (pos.Y > editor.Camera.ViewRect.Y)
					pos.Y -= mtex.Height;
			}

			if (BlendMode == "additive") {
				Draw.SpriteBatch.End();
				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, editor.Camera.Matrix);
			}

			Vector2 drawPos = pos;
			do {
				do {
					mtex.Draw(drawPos, Vector2.Zero, Color, 1f, 0f, flip);
					if (!LoopY)
						break;
					drawPos.Y += mtex.Height;
				} while (drawPos.Y < editor.Camera.ViewRect.Bottom);
				if (!LoopX)
					break;
				drawPos.X += mtex.Width;
				drawPos.Y = pos.Y;
			} while (drawPos.X < editor.Camera.ViewRect.Right);

			if (BlendMode == "additive") {
				Draw.SpriteBatch.End();
				Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, editor.Camera.Matrix);
			}
		}
	}
}
