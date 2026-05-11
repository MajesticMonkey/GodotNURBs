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

		
		public override void _EnterTree()
		{
			Loc = LocFromName();
		}

		public void CreateSurface()
		{
			if (!HasSurface)
			{
				HasSurface = true;
				NotifyPropertyListChanged();
				var forklift = GetParent();
				if (forklift is BezierSurfaceBuilder builder)
				{
					builder.CreateSurfaceExternally(Loc);
				}
				RotationDegrees = new Vector3(90, 0, 0);
			}
			else
			{
				NotifyPropertyListChanged();
			}
		}

		public void RemoveSurface()
		{
			if (HasSurface)
			{
				HasSurface = false;
				NotifyPropertyListChanged();
				var forklift = GetParent();
				if (forklift is BezierSurfaceBuilder builder)
				{
					builder.RemoveSurfaceExternally(Loc);
				}
				RotationDegrees = new Vector3(0, 0, 0);
			}
			else
			{
				NotifyPropertyListChanged();
			}
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
