﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BlisterUI.Widgets {
    public static class Alignment {
        public const int MIN = 0;
        public const int MID = 1;
        public const int MAX = 2;

        public const int LEFT = MIN;
        public const int RIGHT = MAX;
        public const int TOP = MIN;
        public const int BOTTOM = MAX;
    }

    public abstract class BaseWidget : IDisposable {
        // Parent Hierarchy (Should Not Have Cycles)
        private BaseWidget parent;
        public BaseWidget Parent {
            get { return parent; }
            set {
                if(parent == value)
                    return;
                if(parent != null)
                    parent.OnRecompute -= OnParentRecompute;
                parent = value;
                if(parent != null)
                    parent.OnRecompute += OnParentRecompute;
                Recompute();
            }
        }
        public event Action<BaseWidget> OnRecompute;

        // Offset From Parent Calculation Point
        protected Point offset;
        public Point Offset {
            get { return offset; }
            set {
                offset = value;
                Recompute();
            }
        }

        // Another Way To Calculate Anchoring
        protected Point offAlign;
        public int OffsetAlignX {
            get { return offAlign.X; }
            set {
                offAlign.X = value;
                Recompute();
            }
        }
        public int OffsetAlignY {
            get { return offAlign.Y; }
            set {
                offAlign.Y = value;
                Recompute();
            }
        }

        // Anchor Point
        protected Point anchor;
        public Point Anchor {
            get { return anchor; }
            set {
                anchor = value;
                Recompute();
            }
        }

        // Alignment From Anchor (0/1/2)
        protected Point align;
        public int AlignX {
            get { return align.X; }
            set {
                align.X = value;
                Recompute();
            }
        }
        public int AlignY {
            get { return align.Y; }
            set {
                align.Y = value;
                Recompute();
            }
        }

        // Where To Draw To Screen
        private WidgetRenderer renderer;
        public abstract int X {
            get;
            protected set;
        }
        public abstract int Y {
            get;
            protected set;
        }
        public abstract int Width {
            get;
            set;
        }
        public abstract int Height {
            get;
            set;
        }
        public abstract float LayerDepth {
            get;
            set;
        }

        public BaseWidget(WidgetRenderer r) {
            renderer = r;
            anchor = new Point(0, 0);
            align = new Point(Alignment.LEFT, Alignment.TOP);
            PreInit();
            Recompute();
            AddAllDrawables(renderer);
        }
        public void Dispose() {
            RemoveAllDrawables(renderer);
        }
        protected abstract void DisposeOther();

        public abstract void PreInit();
        
        public abstract void AddAllDrawables(WidgetRenderer r);
        public abstract void RemoveAllDrawables(WidgetRenderer r);

        public Point GetOffset(int x, int y) {
            return new Point(x - X, y - Y);
        }
        public bool Inside(int x, int y, out Vector2 ratio) {
            Point p = GetOffset(x, y);
            ratio = new Vector2((float)p.X / (float)Width, (float)p.Y / (float)Height);
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }

        protected virtual void Recompute() {
            if(parent != null) {
                // Get Anchor Via The Parent
                anchor.X = parent.X + ((offAlign.X * parent.Width) / 2) + offset.X;
                anchor.Y = parent.Y + ((offAlign.Y * parent.Height) / 2) + offset.Y;
            }

            // Use Alignment For Computation
            X = anchor.X - ((align.X * Width) / 2);
            Y = anchor.Y - ((align.Y * Height) / 2);

            if(OnRecompute != null)
                OnRecompute(this);
        }
        protected virtual void OnParentRecompute(BaseWidget w) {
            Recompute();
        }
    }
}