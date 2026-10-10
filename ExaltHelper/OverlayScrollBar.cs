using System;
using System.Drawing;

namespace ExaltHelper;

// The same row count and bounds drive painting, wheel input, and thumb dragging.
internal sealed class OverlayScrollBar
{
	public int Offset;

	public Rectangle Track { get; private set; }

	public int MaximumOffset { get; private set; }

	public int VisibleRows { get; private set; }

	public bool Visible => !Track.IsEmpty;

	public Rectangle Thumb
	{
		get
		{
			if (!Visible) return Rectangle.Empty;
			int height = Math.Min(Track.Height, Math.Max(16,
				(int)((double)VisibleRows / (VisibleRows + MaximumOffset) * Track.Height)));
			int top = Track.Top + (int)Math.Round((double)Offset / MaximumOffset * (Track.Height - height));
			return new Rectangle(Track.X + 1, top, Track.Width - 2, height);
		}
	}

	public void ResetLayout()
	{
		Track = Rectangle.Empty;
	}

	public void Configure(Rectangle track, int rowCount, int visibleRows)
	{
		VisibleRows = Math.Max(1, visibleRows);
		MaximumOffset = Math.Max(0, rowCount - VisibleRows);
		Offset = Math.Max(0, Math.Min(MaximumOffset, Offset));
		Track = MaximumOffset > 0 && track.Width > 2 && track.Height > 0 ? track : Rectangle.Empty;
	}

	public void Scroll(int rows)
	{
		Offset = Math.Max(0, Math.Min(MaximumOffset, Offset + rows));
	}

	public void DragTo(int mouseY, int grabOffset)
	{
		int travel = Track.Height - Thumb.Height;
		if (!Visible || travel <= 0) return;
		double fraction = Math.Max(0, Math.Min(1, (double)(mouseY - grabOffset - Track.Top) / travel));
		Offset = (int)Math.Round(fraction * MaximumOffset);
	}
}
