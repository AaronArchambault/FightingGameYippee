using System;

namespace FightCore
{
    //this is one box in sim units where 1000 units is one unity unit
    //cx and cy are the center and cx is measured forward from the fighter so it flips when they turn around
    [Serializable]
    public struct BoxRect
    {
        public int cx, cy, w, h;

        public BoxRect(int cx, int cy, int w, int h) { this.cx = cx; this.cy = cy; this.w = w; this.h = h; }

        //it turns the local box into a world box using where the fighter is and which way they face
        public WorldBox ToWorld(int ox, int oy, int facing)
        {
            int c = ox + cx * facing;
            return new WorldBox(c - w / 2, c + w / 2, oy + cy - h / 2, oy + cy + h / 2);
        }

        public bool IsEmpty { get { return w <= 0 || h <= 0; } }
    }

    //this is a box already placed in the world so it is easy to check overlaps
    public struct WorldBox
    {
        public int left, right, bottom, top;
        public WorldBox(int l, int r, int b, int t) { left = l; right = r; bottom = b; top = t; }

        public bool Overlaps(WorldBox o)
        {
            return left < o.right && right > o.left && bottom < o.top && top > o.bottom;
        }

        public int CenterX { get { return (left + right) / 2; } }
        public int CenterY { get { return (bottom + top) / 2; } }
    }

    //this is a box that only exists on some frames of a move
    //hitGroup is so a move with more than one hit can hit again on a new group
    [Serializable]
    public class FrameBox
    {
        public int start;
        public int end;
        public BoxRect rect;
        public int hitGroup;

        public FrameBox() { }
        public FrameBox(int start, int end, BoxRect rect, int hitGroup = 0)
        {
            this.start = start; this.end = end; this.rect = rect; this.hitGroup = hitGroup;
        }

        public bool ActiveOn(int frame) { return frame >= start && frame <= end; }
    }
}
