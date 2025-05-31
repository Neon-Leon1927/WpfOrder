using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BestDelivery;
using GeoPoint = BestDelivery.Point;
using DisplayPoint = System.Windows.Point;

namespace WpfOrder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Order[] activeParcels = Array.Empty<Order>();
        private GeoPoint hubLocation;
        private int[] deliveryOrder = Array.Empty<int>();
        private readonly Random rnd = new Random();
        public MainWindow()
        {
            InitializeComponent();
        }
        private void HandleDeliveryScenario(Func<Order[]> fetchOrders, string description)
        {
            activeParcels = fetchOrders();
            hubLocation = activeParcels.First(o => o.ID == -1).Destination;
            deliveryOrder = CreateOptimizedRoute(activeParcels, hubLocation);
            RefreshlistViewOrders();
            UpdateRouteInfo();
        }

        private void center_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Центр");
        private void far_center_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Дальше от центра");
        private void district_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Один район");
        private void different_parts_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Разные районы");
        private void different_priority_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Разные приоритеты");
        private void more_orders_Click(object sender, RoutedEventArgs e) =>
    HandleDeliveryScenario(OrderArrays.GetOrderArray1, "Много заказов");

        private void RefreshlistViewOrders()
        {
            listViewOrders.Items.Clear();
            foreach (var order in activeParcels)
            {
                string displayText = order.ID == -1
                    ? $"СКЛАД: ({order.Destination.X:F2}, {order.Destination.Y:F2})"
                    : $"Заказ #{order.ID}: ({order.Destination.X:F2}, {order.Destination.Y:F2}), Приоритет: {order.Priority:F2}";

                listViewOrders.Items.Add(displayText);
            }
        }

        private void UpdateRouteInfo()
        {
            if (Valid(hubLocation, activeParcels, deliveryOrder, out double routeLength))
            {
                Cost.Text = $"Оптимальный путь: {routeLength:F2}";
                Route.Text = "Маршрут: " + string.Join(" → ", deliveryOrder.Select(id => id == -1 ? "СКЛАД" : "#" + id));
                DrawRoute();
            }
            else
            {
                Cost.Text = "Маршрут недействителен";
                Route.Text = "";
                canvas.Children.Clear();
            }
        }
        private void DrawRoute()
        {
            canvas.Children.Clear();
            var positions = new Dictionary<int, DisplayPoint>();

            double margin = 50;
            double width = canvas.ActualWidth > 0 ? canvas.ActualWidth : 800;
            double height = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;

            var points = activeParcels.Select(p => p.Destination).ToList();
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double scaleX = (width - 2 * margin) / (maxX - minX);
            double scaleY = (height - 2 * margin) / (maxY - minY);
            double scale = Math.Min(scaleX, scaleY);

            double shiftX = margin - minX * scale;
            double shiftY = height - margin + minY * scale;

            DisplayPoint Map(GeoPoint p) => new(p.X * scale + shiftX, shiftY - p.Y * scale);

            foreach (var order in activeParcels)
            {
                var point = Map(order.Destination);
                positions[order.ID] = point;

                var marker = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = order.ID == -1 ? Brushes.Red : Brushes.Blue,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5
                };

                Canvas.SetLeft(marker, point.X - 6);
                Canvas.SetTop(marker, point.Y - 6);
                canvas.Children.Add(marker);
            }

            for (int i = 0; i < deliveryOrder.Length - 1; i++)
            {
                var from = positions[deliveryOrder[i]];
                var to = positions[deliveryOrder[i + 1]];
                var line = new Line
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    Stroke = Brushes.DarkGreen,
                    StrokeThickness = 2
                };
                canvas.Children.Add(line);
            }
        }
        private void RouteCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(canvas);

            double margin = 50;
            double width = canvas.ActualWidth > 0 ? canvas.ActualWidth : 800;
            double height = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;

            var points = activeParcels.Select(p => p.Destination).ToList();
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double scaleX = (width - 2 * margin) / (maxX - minX);
            double scaleY = (height - 2 * margin) / (maxY - minY);
            double scale = Math.Min(scaleX, scaleY);

            double shiftX = margin - minX * scale;
            double shiftY = height - margin + minY * scale;

            double x = (pos.X - shiftX) / scale;
            double y = (shiftY - pos.Y) / scale;

            // Ввод приоритета
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите приоритет (от 0.0 до 1.0)",
                "Новый заказ",
                "0.5"
            );

            if (!double.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double priority) ||
                priority < 0.0 || priority > 1.0)
            {
                MessageBox.Show("Приоритет должен быть числом от 0.0 до 1.0",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newId = activeParcels.Max(o => o.ID) + 1;
            var newOrder = new Order
            {
                ID = newId,
                Destination = new GeoPoint { X = x, Y = y },
                Priority = priority
            };

            var list = activeParcels.ToList();
            list.Add(newOrder);
            activeParcels = list.ToArray();

            deliveryOrder = CreateOptimizedRoute(activeParcels, hubLocation);
            RefreshlistViewOrders();
            UpdateRouteInfo();

            Status.Text = $"Добавлен заказ #{newId} с приоритетом {priority:F2}";
        }
        public static int[] CreateOptimizedRoute(Order[] parcels, GeoPoint hub)
        {
            var orders = parcels.Where(p => p.ID != -1).ToList();
            if (orders.Lenght = 0) return new[] { -1, -1 };

            var points = new List<GeoPoint> { hub };
            points.AddRange(orders.Select(o => o.Destination));

            int n = points.Count;
            double[,] dist = new double[n, n];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++                                                                                           )
                    dist[i, j] = i == j ? 0 : Math.Sqrt(Math.Pow(points[i].X - points[j].X, 2) + Math.Pow(points[i].Y - points[j].Y, 2));

            List<int> route = new() { 0 };
            var unvisited = Enumerable.Range(1, n - 1).ToList();

            while (unvisited.Count > 0)
            {
                int last = route.Last();
                int next = unvisited.OrderBy(i => dist[last, i]).First();
                route.Add(next);
                unvisited.Remove(next);
            }

            route.Add(0);

            var result = new List<int> { -1 };
            for (int i = 1; i < route.Count - 1; i++)
                result.Add(orders[route[i] - 1].ID);
            result.Add(-1);
            return result.ToArray();
        }
        public static bool Valid(GeoPoint hub, Order[] parcels, int[] route, out double routeLength)
        {
            routeLength = 0;
            if (parcels == null || route == null || parcels.Length == 0 || route.Length == 0) return false;

            var routeList = new List<int>(route);
            if (routeList.First() != -1 || routeList.Last() != -1) return false;

            var allIds = parcels.Where(p => p.ID != -1).Select(p => p.ID).ToHashSet();
            var visited = routeList.Where(id => id != -1).ToHashSet();
            if (!allIds.SetEquals(visited)) return false;

            GeoPoint current = hub;
            foreach (var id in routeList.Skip(1))
            {
                var o = parcels.First(p => p.ID == id);
                routeLength += Math.Sqrt(Math.Pow(current.X - o.Destination.X, 2) + Math.Pow(current.Y - o.Destination.Y, 2));
                current = o.Destination;
            }
            return true;
        }
    }
}
