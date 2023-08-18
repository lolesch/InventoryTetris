
using UnityEngine;

namespace ToolSmiths.InventorySystem.Utility.Extensions
{
    public static class VectorExtensions
    {
        #region Conversions

        ///// <summary>
        ///// Converts a <see cref="Coordinate"/> into a <see cref="Vector3"/> with settable <paramref name="y"/> value.
        ///// </summary>
        ///// <param name="coordinate"></param>
        ///// <param name="y"></param>
        ///// <returns><see cref="Vector3"/>(<paramref name="coordinate"/>.x, <paramref name="y"/>, <paramref name="coordinate"/>.z)</returns>
        //public static Vector3 ToVector3(this Coordinate coordinate, float y = 0f) => new(coordinate.x, y, coordinate.z);
        //
        ///// <summary>
        ///// Converts a <see cref="Vector3"/> into a <see cref="Coordinate"/> dropping the <paramref name="y"/> value.
        ///// </summary>
        ///// <param name="vector3"></param>
        ///// <param name="y"></param>
        ///// <returns><see cref="Coordinate"/>(<paramref name="vector3"/>.x, <paramref name="vector3"/>.z)</returns>
        //public static Coordinate ToCoordinate(this Vector3 vector3) => new(vector3.x, vector3.z);

        // convert a vector from coordinate space to another coordinate space
        /* spaces:
        * - screenSpace
        * - windowSpace
        * - worldSpace
        * - cameraSpace
        * - viewport?
        * - ...
        */
        #endregion Conversions

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns>The normalized direction (<paramref name="from"/> -> <paramref name="to"/>).</returns>
        public static Vector3 Direction(this Vector3 from, Vector3 to) => Vector3.Normalize(to - from);

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="from"></param>
        ///// <param name="to"></param>
        ///// <returns>The normalized direction (<paramref name="from"/> -> <paramref name="to"/>).</returns>
        //public static Coordinate Direction(this Coordinate from, Coordinate to)
        //{
        //    var distance = to - from;
        //    var num = Coordinate.Magnitude(distance);
        //    return num > 1E-05f ? distance / num : new Coordinate();
        //}

        /// <summary>
        /// Project a vector on a rotated cartesian coordinate system.
        /// </summary>
        /// <param name="vector3"></param>
        /// <param name="yDegrees"></param>
        /// <returns><see cref="Vector3"/>() rotated by <paramref name="yDegrees"/> around the global upAxis.</returns>
        public static Vector3 ToIsometric(this Vector3 vector3, float yDegrees = 45f)
        {
            var isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0f, yDegrees, 0f));

            return isoMatrix.MultiplyPoint3x4(vector3);
        }
    }
}