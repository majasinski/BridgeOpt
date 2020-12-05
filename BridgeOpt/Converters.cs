using System;
using Autodesk.Revit.DB;

namespace BridgeOpt
{
    class Converters
    {
        public static double ToMillimeters(double length)
        {
            return UnitUtils.ConvertFromInternalUnits(length, DisplayUnitType.DUT_MILLIMETERS);
        }
        public static double ToCentimeters(double length)
        {
            return UnitUtils.ConvertFromInternalUnits(length, DisplayUnitType.DUT_CENTIMETERS);
        }
        public static double ToMeters(double length)
        {
            return UnitUtils.ConvertFromInternalUnits(length, DisplayUnitType.DUT_METERS);
        }

        public static double ToCubicoMillimeters(double volume)
        {
            return UnitUtils.ConvertFromInternalUnits(volume, DisplayUnitType.DUT_CUBIC_MILLIMETERS);
        }
        public static double ToCubicCentimeters(double volume)
        {
            return UnitUtils.ConvertFromInternalUnits(volume, DisplayUnitType.DUT_CUBIC_CENTIMETERS);
        }
        public static double ToCubicMeters(double volume)
        {
            return UnitUtils.ConvertFromInternalUnits(volume, DisplayUnitType.DUT_CUBIC_METERS);
        }

        public static double FromMillimeters(double length)
        {
            return UnitUtils.ConvertToInternalUnits(length, DisplayUnitType.DUT_MILLIMETERS);
        }
        public static double FromCentimeters(double length)
        {
            return UnitUtils.ConvertToInternalUnits(length, DisplayUnitType.DUT_CENTIMETERS);
        }
        public static double FromMeters(double length)
        {
            return UnitUtils.ConvertToInternalUnits(length, DisplayUnitType.DUT_METERS);
        }

        public static double FromCubicoMillimeters(double volume)
        {
            return UnitUtils.ConvertToInternalUnits(volume, DisplayUnitType.DUT_CUBIC_MILLIMETERS);
        }
        public static double FromCubicCentimeters(double volume)
        {
            return UnitUtils.ConvertToInternalUnits(volume, DisplayUnitType.DUT_CUBIC_CENTIMETERS);
        }
        public static double FromCubicMeters(double volume)
        {
            return UnitUtils.ConvertToInternalUnits(volume, DisplayUnitType.DUT_CUBIC_METERS);
        }
    }
}