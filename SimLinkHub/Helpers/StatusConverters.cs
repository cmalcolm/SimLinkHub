using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SimLinkHub.Helpers
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            (bool)v ? Brushes.LimeGreen : Brushes.Crimson;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class ConnectionTextConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            (bool)v ? "CONNECTED" : "DISCONNECTED";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}