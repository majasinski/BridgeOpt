using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.Revit.DB;

namespace BridgeOpt
{
    public class QuantitiesAndSchedules
    {
        private class QuantitiesDefaultPrices
        {
            public string ScheduleName;
            public double SchedulePrice;

            public QuantitiesDefaultPrices(string scheduleName, double schedulePrice)
            {
                ScheduleName = scheduleName;
                SchedulePrice = schedulePrice;
            }
        }
        public PhysicalBridge PhysicalBridge;
        public List<ViewSchedule> Schedules = new List<ViewSchedule>();
        public List<double> Quantities = new List<double>();

        public List<double> Costs = new List<double>();
        public double TotalCost;

        private readonly List<string> IgnoredSchedules = new List<string>()
        {
            "Beton niekonstrukcyjny. Beton w deskowaniu",
            "Beton. Beton podpór w elementach o grubości do 60 cm. Filary",
            "Beton. Beton płyt przejściowych",
            "Beton. Beton zabudów chodnikowych",
            "Beton. Prefabrykaty betonowe. Deski gzymsowe",
            "Inne roboty mostowe. Umocnienie skarp i stożków betonowymi płytami ażurowymi",
            "Izolacje i nawierzchnie. Izolacja bezszwowa",
            "Izolacje i nawierzchnie. Nawierzchnie epoksydowo-poliuretanowe",
            "Izolacje i nawierzchnie. Warstwa wiążąca z betonu asfaltowego",
            "Izolacje i nawierzchnie. Warstwa ścieralna SMA",
            "Roboty jednostkowe",
            "Zbrojenie. Zbrojenie płyt przejściowych",
            "Zbrojenie. Zbrojenie zabudów chodnikowych"
        };
        private readonly List<QuantitiesDefaultPrices> DefaultPrices = new List<QuantitiesDefaultPrices>()
        {
            new QuantitiesDefaultPrices("Beton niekonstrukcyjny. Beton bez deskowania", 330.0), //Kalkulacja własna, na podstawie SKANSKA EH, poz. 5.4.13
            new QuantitiesDefaultPrices("Beton. Beton ciosów", 590.0), //Kalkulacja własna, na podstawie SKANSKA EH, poz. 11b
            new QuantitiesDefaultPrices("Beton. Beton fundamentów w deskowaniu", 620.0), //BCD M-21.20.01.13.01
            new QuantitiesDefaultPrices("Beton. Beton podpór w elementach o grubości do 60 cm. Ściany", 850.0), //BCD M-22.01.02.13.01
            new QuantitiesDefaultPrices("Beton. Beton podpór w elementach o grubości powyżej 60 cm. Filary", 1830.0), //BCD M-22.02.05.13.02
            new QuantitiesDefaultPrices("Beton. Beton podpór w elementach o grubości powyżej 60 cm. Ściany", 890.0), //BCD M-22.01.01.13.02
            new QuantitiesDefaultPrices("Beton. Beton ustroju nośnego", 1090.0), //BCD M-23.01.02.15.04
            new QuantitiesDefaultPrices("Fundamentowanie. Grunt zasypowy za przyczółkami, stożki", 85.0), //BCD M-29.03.01.11.04
            new QuantitiesDefaultPrices("Fundamentowanie. Wykopy pod fundamenty", 58.0), //BCD M-21.30.01.11.01
            new QuantitiesDefaultPrices("Fundamentowanie. Zasypanie wykopów z zagęszczeniem", 85.0), //Kalkulacja własna, przyjmowana na podstawie kosztu gruntu zasypowego za przyczółkami (BCD M-29.03.01.11.04)
            new QuantitiesDefaultPrices("Inne roboty mostowe. Zabezpieczenie antykorozyjne powierzchni betonowych", 75.0), //BCD M-30.20.05.11.01
            new QuantitiesDefaultPrices("Izolacje i nawierzchnie. Zabezpieczenie powierzchni betonowych masą cienkopowłokową", 30.8), //Kalkulacja własna, na podstawie SKANSKA EH, poz. 47a
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie ciosów", 5.5), //Kalkulacja własna, przyjmowana na podstawie kosztu zbrojenia filarów (BCD M-22.02.05.69.01)
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie filarów", 5.5), //BCD M-22.02.05.69.01
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie fundamentów", 5.1), //BCD M-21.20.01.69.01
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie ścian przyczółków", 5.5), //BCD M-22.01.01.69.01, BCD M-22.01.02.69.01
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie ustroju nośnego. Sprężenie", 17.8), //BCD M-23.02.01.68.01
            new QuantitiesDefaultPrices("Zbrojenie. Zbrojenie ustroju nośnego. Zbrojenie miękkie", 5.2) //BCD M-23.01.02.69.01
        };

        public QuantitiesAndSchedules(PhysicalBridge bridge)
        {
            PhysicalBridge = bridge;
        }

        public void LoadSchedules(bool excludeIgnored = true)
        {
            Document doc = PhysicalBridge.CommandData.Application.ActiveUIDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> collection = collector.OfClass(typeof(ViewSchedule)).ToElements();

            List<string> scheduleNames = new List<string>();
            foreach (Element e in collection)
            {
                if (excludeIgnored == false) scheduleNames.Add(e.Name);
                else if (!IgnoredSchedules.Contains(e.Name)) scheduleNames.Add(e.Name);
            }

            scheduleNames.Sort();

            Schedules.Clear();
            PhysicalBridge.DataForm.DataFormSchedules.Rows.Clear();
            foreach (string scheduleName in scheduleNames)
            {
                ViewSchedule schedule = GetScheduleByName(scheduleName);
                PhysicalBridge.DataForm.DataFormSchedules.Rows.Add(scheduleName, "0.0", GetScheduleUnit(schedule));
                foreach (QuantitiesDefaultPrices price in DefaultPrices)
                {
                    if (price.ScheduleName.Equals(scheduleName))
                    {
                        PhysicalBridge.DataForm.DataFormSchedules.Rows[PhysicalBridge.DataForm.DataFormSchedules.Rows.Count - 2].Cells[1].Value = string.Format("{0:0.0}", price.SchedulePrice);
                        break;
                    }
                }
                Schedules.Add(schedule);
            }
            PhysicalBridge.DataForm.DataFormScheduleItemsLabel.Text = "Number of items: " + (PhysicalBridge.DataForm.DataFormSchedules.Rows.Count - 1).ToString();
        }

        public ViewSchedule GetScheduleByName(string scheduleName)
        {
            Document doc = PhysicalBridge.CommandData.Application.ActiveUIDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> collection = collector.OfClass(typeof(ViewSchedule)).ToElements();
            foreach (Element e in collection)
            {
                if (e.Name.Equals(scheduleName)) return e as ViewSchedule;
            }
            return null;
        }

        public double GetScheduleQuantity(ViewSchedule schedule)
        {
            TableData table = schedule.GetTableData();
            TableSectionData section = table.GetSectionData(SectionType.Body);
            if (section.NumberOfRows > 0)
            {
                for (int col = section.NumberOfColumns - 1; col >= 0; col--)
                {
                    string strValue = schedule.GetCellText(SectionType.Body, section.NumberOfRows - 1, col);
                    if (strValue.Length > 0)
                    {
                        if (strValue.Contains(" "))
                        {
                            strValue = strValue.Remove(strValue.IndexOf(" "));
                            if (double.TryParse(strValue, out double value)) return value;
                        }
                        else if (double.TryParse(strValue, out double value)) return value;
                    }
                }
            }
            return 0.0;
        }

        public string GetScheduleUnit(ViewSchedule schedule)
        {
            TableData table = schedule.GetTableData();
            TableSectionData section = table.GetSectionData(SectionType.Body);
            if (section.NumberOfRows > 0)
            {
                for (int col = section.NumberOfColumns - 1; col >= 0; col--)
                {
                    string unit = schedule.GetCellText(SectionType.Body, section.NumberOfRows - 1, col);
                    if (unit.Length > 0)
                    {
                        if (unit.Contains(" ")) return unit.Remove(0, unit.IndexOf(" ") + 1);
                        else return "kg";
                    }
                }
            }
            return "";
        }

        public double GetSchedulePrice(ViewSchedule schedule)
        {
            if (PhysicalBridge.DataForm.DataFormSchedules.Rows.Count > 1)
            {
                foreach (DataGridViewRow row in PhysicalBridge.DataForm.DataFormSchedules.Rows)
                {
                    if (((string) row.Cells[0].Value).Equals(schedule.Name))
                    {
                        if (double.TryParse((string) row.Cells[1].Value, out double price)) return price;
                        return 0.0;
                    }
                }
            }
            return 0.0;
        }

        public double Calculate(bool print)
        {
            Quantities.Clear();
            Costs.Clear();
            foreach (ViewSchedule schedule in Schedules)
            {
                Quantities.Add(GetScheduleQuantity(schedule));
                Costs.Add(Quantities.Last() * GetSchedulePrice(schedule));
            }
            TotalCost = Costs.Sum();

            if (print)
            {
                using (StreamWriter costSummary = new StreamWriter(PhysicalBridge.Directory + Globals.TextFiles.CostSummary, false))
                {
                    for (int i = 0; i < Schedules.Count(); i++)
                    {
                        costSummary.WriteLine(Schedules[i].Name);
                        costSummary.WriteLine(string.Format("{0:0.0}\t{1:0.0}\t{2:0.0}", Quantities[i], GetSchedulePrice(Schedules[i]), Costs[i]));
                    }
                    costSummary.WriteLine(string.Format("\nTotal cost:\t{0:0.0}", TotalCost));
                }
            }
            return TotalCost;
        }
    }
}