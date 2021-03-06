﻿// <copyright file="PixelAccessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides per-pixel access to generic <see cref="Image{TColor,TPacked}"/> pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public unsafe class PixelAccessor<TColor, TPacked> : IDisposable
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The pointer to the pixel buffer.
        /// </summary>
        private IntPtr dataPointer;

        /// <summary>
        /// The position of the first pixel in the bitmap.
        /// </summary>
        private byte* pixelsBase;

        /// <summary>
        /// Provides a way to access the pixels from unmanaged memory.
        /// </summary>
        private GCHandle pixelsHandle;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose() method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessor{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="image">The image to provide pixel access for.</param>
        public PixelAccessor(ImageBase<TColor, TPacked> image)
        {
            Guard.NotNull(image, nameof(image));
            Guard.MustBeGreaterThan(image.Width, 0, "image width");
            Guard.MustBeGreaterThan(image.Height, 0, "image height");

            this.Width = image.Width;
            this.Height = image.Height;
            this.pixelsHandle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
            this.dataPointer = this.pixelsHandle.AddrOfPinnedObject();
            this.pixelsBase = (byte*)this.dataPointer.ToPointer();
            this.PixelSize = Unsafe.SizeOf<TPacked>();
            this.RowStride = this.Width * this.PixelSize;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PixelAccessor{TColor,TPacked}"/> class.
        /// </summary>
        ~PixelAccessor()
        {
            this.Dispose();
        }

        /// <summary>
        /// Gets the pointer to the pixel buffer.
        /// </summary>
        public IntPtr DataPointer => this.dataPointer;

        /// <summary>
        /// Gets the size of a single pixel in the number of bytes.
        /// </summary>
        public int PixelSize { get; }

        /// <summary>
        /// Gets the width of one row in the number of bytes.
        /// </summary>
        public int RowStride { get; }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than zero and smaller than the width of the pixel.</param>
        /// <returns>The <see cref="TColor"/> at the specified position.</returns>
        public TColor this[int x, int y]
        {
            get { return Unsafe.Read<TColor>(this.pixelsBase + (((y * this.Width) + x) * Unsafe.SizeOf<TColor>())); }
            set { Unsafe.Write(this.pixelsBase + (((y * this.Width) + x) * Unsafe.SizeOf<TColor>()), value); }
        }

        /// <summary>
        /// Copies a block of pixels at the specified position.
        /// </summary>
        /// <param name="sourceX">The x-coordinate of the source image.</param>
        /// <param name="sourceY">The y-coordinate of the source image.</param>
        /// <param name="target">The target pixel buffer accessor.</param>
        /// <param name="targetX">The x-coordinate of the target image.</param>
        /// <param name="targetY">The y-coordinate of the target image.</param>
        /// <param name="pixelCount">The number of pixels to copy</param>
        public void CopyBlock(int sourceX, int sourceY, PixelAccessor<TColor, TPacked> target, int targetX, int targetY, int pixelCount)
        {
            int size = Unsafe.SizeOf<TColor>();
            byte* sourcePtr = this.pixelsBase + (((sourceY * this.Width) + sourceX) * size);
            byte* targetPtr = target.pixelsBase + (((targetY * target.Width) + targetX) * size);
            uint byteCount = (uint)(pixelCount * size);

            Unsafe.CopyBlock(targetPtr, sourcePtr, byteCount);
        }

        /// <summary>
        /// Copies an entire image.
        /// </summary>
        /// <param name="target">The target pixel buffer accessor.</param>
        public void CopyImage(PixelAccessor<TColor, TPacked> target)
        {
            this.CopyBlock(0, 0, target, 0, 0, target.Width * target.Height);
        }

        public void CopyFrom(PixelRow<TColor, TPacked> row, int targetY)
        {
            switch (row.ComponentOrder)
            {
                case ComponentOrder.BGR:
                    this.CopyFromBGR(row, targetY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.BGRA:
                    this.CopyFromBGRA(row, targetY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.RGB:
                    this.CopyFromRGB(row, targetY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.RGBA:
                    this.CopyFromRGBA(row, targetY, Math.Min(row.Width, this.Width));
                    break;
                default:
                  throw new NotSupportedException();
            }
        }

        public void CopyTo(PixelRow<TColor, TPacked> row, int sourceY)
        {
            switch (row.ComponentOrder)
            {
                case ComponentOrder.BGR:
                    this.CopyToBGR(row, sourceY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.BGRA:
                    this.CopyToBGRA(row, sourceY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.RGB:
                    this.CopyToRGB(row, sourceY, Math.Min(row.Width, this.Width));
                    break;
                case ComponentOrder.RGBA:
                    this.CopyToRGBA(row, sourceY, Math.Min(row.Width, this.Width));
                    break;
                default:
                  throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.pixelsHandle.IsAllocated)
            {
                this.pixelsHandle.Free();
            }

            this.dataPointer = IntPtr.Zero;
            this.pixelsBase = null;

            // Note disposing is done.
            this.isDisposed = true;

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void CopyFromBGR(PixelRow<TColor, TPacked> row, int targetY, int width)
        {
            byte* source = row.DataPointer;
            byte* destination = this.GetRowPointer(targetY);

            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int x = 0; x < width; x++)
            {
                packed.PackFromBytes(*(source + 2), *(source + 1), *source, 255);
                Unsafe.Write(destination, packed);

                source += 3;
                destination += size;
            }
        }

        protected virtual void CopyFromBGRA(PixelRow<TColor, TPacked> row, int targetY, int width)
        {
            byte* source = row.DataPointer;
            byte* destination = this.GetRowPointer(targetY);

            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int x = 0; x < width; x++)
            {
                packed.PackFromBytes(*(source + 2), *(source + 1), *source, *(source + 3));
                Unsafe.Write(destination, packed);

                source += 4;
                destination += size;
            }
        }

        protected virtual void CopyFromRGB(PixelRow<TColor, TPacked> row, int targetY, int width)
        {
            byte* source = row.DataPointer;
            byte* destination = this.GetRowPointer(targetY);

            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int x = 0; x < width; x++)
            {
                packed.PackFromBytes(*source, *(source + 1), *(source + 2), 255);
                Unsafe.Write(destination, packed);

                source += 3;
                destination += size;
            }
        }

        protected virtual void CopyFromRGBA(PixelRow<TColor, TPacked> row, int targetY, int width)
        {
            byte* source = row.DataPointer;
            byte* destination = this.GetRowPointer(targetY);

            TColor packed = default(TColor);
            int size = Unsafe.SizeOf<TColor>();

            for (int x = 0; x < width; x++)
            {
                packed.PackFromBytes(*source, *(source + 1), *(source + 2), *(source + 3));
                Unsafe.Write(destination, packed);

                source += 4;
                destination += size;
            }
        }

        protected virtual void CopyToBGR(PixelRow<TColor, TPacked> row, int sourceY, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                this[x, sourceY].ToBytes(row.Data, offset, ComponentOrder.BGR);
                offset += 3;
            }
        }

        protected virtual void CopyToBGRA(PixelRow<TColor, TPacked> row, int sourceY, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                this[x, sourceY].ToBytes(row.Data, offset, ComponentOrder.BGRA);
                offset += 4;
            }
        }

        protected virtual void CopyToRGB(PixelRow<TColor, TPacked> row, int sourceY, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                this[x, sourceY].ToBytes(row.Data, offset, ComponentOrder.RGB);
                offset += 3;
            }
        }

        protected virtual void CopyToRGBA(PixelRow<TColor, TPacked> row, int sourceY, int width)
        {
            int offset = 0;
            for (int x = 0; x < width; x++)
            {
                this[x, sourceY].ToBytes(row.Data, offset, ComponentOrder.RGBA);
                offset += 4;
            }
        }

        protected byte* GetRowPointer(int targetY)
        {
            return this.pixelsBase + ((targetY * this.Width) * Unsafe.SizeOf<TColor>());
        }
    }
}