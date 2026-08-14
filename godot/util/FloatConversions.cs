using System;
public static class FloatConversions
{
        /// map float (expected 0..1) to byte 0..255.
        public static byte FloatToByte(float v)
        {
                return (byte)MathF.Round(v * 255f);
        }

        public static float ByteToFloat(byte v)
        {
                return v / 255f;
        }
}
