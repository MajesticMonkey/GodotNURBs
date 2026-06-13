using Godot;
using Godot.Collections;
using System;
using BezierSurfaces;

namespace BezierSurfaces
{
	[GlobalClass, Tool]
	public partial class ControlPoint : MeshInstance3D
	{
		public Vector2 Loc = new Vector2(0, 0);

		public bool HasSurface = false;

		public string HasSurfaceMetaName = "HasSurface";

		[Export]
		public float Weight = 1.0f;
		
		public override void _EnterTree()
		{
			Loc = LocFromName();
		}

		public void CreateSurface()
		{
			ToggleSurface(true);
		}

		public void RemoveSurface()
		{
			ToggleSurface(false);
		}

		public void ToggleSurface(bool Add)
		{
			if (HasSurface ^ Add)
			{
				var forklift = GetParent();
				if (forklift is BezierSurfaceBuilder builder)
				{
					HasSurface = !HasSurface;
					if (HasSurface) { builder.CreateSurfaceExternally(Loc); } else { builder.RemoveSurfaceExternally(Loc); }
					char LastChar = ((string)Name)[^1];
					if (HasSurface ^ (LastChar == '_')) { Name = LastChar == '_' ? ((string)Name)[..^1] : ((string)Name) + '_'; }
				}
				else
				{
					throw new InvalidOperationException("This control point has somehow been unparented from a BezierSurfaceBuilder. Please reload your project and hope for the best.");
				}
			}
			NotifyPropertyListChanged();
		}

		public Vector2 LocFromName()
		{
			Vector2 Location = new Vector2(0, 0);
			string MyName = this.Name;
			for (var i = 0; i < MyName.Length; i++)
			{

			}
			string[] SplitName = MyName.Split("_");
			Location.X = float.Parse(SplitName[1]);
			Location.Y = float.Parse(SplitName[2]);
			return Location;
		}
	}
}
