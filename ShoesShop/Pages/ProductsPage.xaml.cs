using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ShoesShop.Pages
{
    public partial class ProductsPage : Page
    {
        private MainWindow _mainWindow;
        private List<Товары> _allProducts;
        private List<Поставщики> _suppliers;
        private bool _isAdmin = false;
        private bool _isManager = false;

        public static readonly BooleanToVisibilityConverter BoolToVisibilityConverter = new BooleanToVisibilityConverter();

        public ProductsPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            Loaded += ProductsPage_Loaded;
        }

        private void ProductsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            SetupUserPermissions();
        }

        private void SetupUserPermissions()
        {
            if (_mainWindow.CurrentUser != null)
            {
                var role = _mainWindow.CurrentUser.Роли?.Роль;
                _isAdmin = role == "Администратор";
                _isManager = role == "Менеджер";

                if (_isManager || _isAdmin)
                {
                    ControlPanel.Visibility = Visibility.Visible;
                    LoadSuppliers();
                    SetupFilters();

                    // Показываем кнопку "Добавить товар" только для администратора
                    AddProductButton.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;

                    // Показываем кнопку "Заказы" для менеджеров и администраторов
                    OrdersButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ControlPanel.Visibility = Visibility.Collapsed;
                    AddProductButton.Visibility = Visibility.Collapsed;
                    OrdersButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ControlPanel.Visibility = Visibility.Collapsed;
                AddProductButton.Visibility = Visibility.Collapsed;
                OrdersButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadProducts()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    _allProducts = context.Товары
                        .Include(t => t.Категории)
                        .Include(t => t.Производители)
                        .Include(t => t.Поставщики)
                        .ToList();

                    // Создаем обертки для товаров с дополнительными свойствами
                    var productWrappers = _allProducts.Select(p => new ProductWrapper(p)).ToList();
                    ProductsItemsControl.ItemsSource = productWrappers;

                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки товаров: {ex.Message}");
            }
        }

        private void LoadSuppliers()
        {
            try
            {
                using (var context = new ShoesShopEntities())
                {
                    _suppliers = context.Поставщики.ToList();
                    SupplierFilterComboBox.Items.Clear();

                    // Добавляем "Все поставщики"
                    var allSuppliers = new Поставщики { ID = -1, Поставщик = "Все поставщики" };
                    SupplierFilterComboBox.Items.Add(allSuppliers);

                    foreach (var supplier in _suppliers)
                    {
                        SupplierFilterComboBox.Items.Add(supplier);
                    }

                    SupplierFilterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _mainWindow.ShowError($"Ошибка загрузки поставщиков: {ex.Message}");
            }
        }

        private void SetupFilters()
        {
            SortComboBox.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (_allProducts == null) return;

            var filteredProducts = _allProducts.AsEnumerable();

            // Поиск
            if (ControlPanel.Visibility == Visibility.Visible && !string.IsNullOrEmpty(SearchTextBox.Text))
            {
                var searchText = SearchTextBox.Text.ToLower();
                filteredProducts = filteredProducts.Where(p =>
                    (p.Наименование_товара?.ToLower().Contains(searchText) ?? false) ||
                    (p.Описание_товара?.ToLower().Contains(searchText) ?? false) ||
                    (p.Категории?.Категория?.ToLower().Contains(searchText) ?? false) ||
                    (p.Производители?.Производитель?.ToLower().Contains(searchText) ?? false) ||
                    (p.Поставщики?.Поставщик?.ToLower().Contains(searchText) ?? false) ||
                    (p.Артикул?.ToLower().Contains(searchText) ?? false));
            }

            // Фильтр по поставщику
            if (ControlPanel.Visibility == Visibility.Visible &&
                SupplierFilterComboBox.SelectedItem is Поставщики selectedSupplier &&
                selectedSupplier.ID != -1)
            {
                filteredProducts = filteredProducts.Where(p => p.Поставщики?.ID == selectedSupplier.ID);
            }

            // Сортировка
            if (ControlPanel.Visibility == Visibility.Visible)
            {
                switch (SortComboBox.SelectedIndex)
                {
                    case 1:
                        filteredProducts = filteredProducts.OrderBy(p => p.Количество_на_складе);
                        break;
                    case 2:
                        filteredProducts = filteredProducts.OrderByDescending(p => p.Количество_на_складе);
                        break;
                    default:
                        filteredProducts = filteredProducts.OrderBy(p => p.ID);
                        break;
                }
            }

            var result = filteredProducts.Select(p => new ProductWrapper(p)).ToList();
            ProductsItemsControl.ItemsSource = result;

            NoProductsText.Visibility = result.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, есть ли страница заказов
            NavigationService.Navigate(new OrdersPage(_mainWindow));
        }

        private void ProductBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((_isAdmin || _isManager) && sender is Border border && border.Tag != null)
            {
                int productId = (int)border.Tag;
                var product = _allProducts.FirstOrDefault(p => p.ID == productId);

                if (product != null)
                {
                    if (_isAdmin)
                    {
                        // Переходим на страницу редактирования для администратора
                        NavigationService.Navigate(new EditProductPage(_mainWindow, product));
                    }
                    else if (_isManager)
                    {
                        // Для менеджера показываем детали товара
                        _mainWindow.ShowMessage($"Детали товара: {product.Наименование_товара}\n" +
                                               $"Цена: {product.Цена:C}\n" +
                                               $"Количество на складе: {product.Количество_на_складе}");
                    }
                }
            }
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new EditProductPage(_mainWindow));
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SupplierFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Класс-обертка для товара с дополнительными свойствами
        public class ProductWrapper
        {
            private readonly Товары _product;

            public ProductWrapper(Товары product)
            {
                _product = product;
            }

            public int ID => _product.ID;

            // Прокси-свойства для прямого доступа
            public string Наименование_товара => _product.Наименование_товара;
            public string Описание_товара => _product.Описание_товара;
            public decimal Цена => _product.Цена;
            public string Единица_измерения => _product.Единица_измерения;
            public int Количество_на_складе => _product.Количество_на_складе;
            public int? Действующая_скидка => _product.Действующая_скидка;
            public int? Скидка => _product.Скидка;
            public string Артикул => _product.Артикул;
            public string Фото => _product.Фото;
            public Категории Категории => _product.Категории;
            public Производители Производители => _product.Производители;
            public Поставщики Поставщики => _product.Поставщики;

            // Дополнительные вычисляемые свойства
            public BitmapImage ImageSource
            {
                get
                {
                    string imageName = _product.Фото;

                    // Получаем корневую папку проекта
                    string projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)
                                                 ?.Parent?.Parent?.FullName;

                    if (string.IsNullOrEmpty(projectRoot))
                    {
                        projectRoot = AppDomain.CurrentDomain.BaseDirectory;
                    }

                    // Если имя файла указано
                    if (!string.IsNullOrEmpty(imageName))
                    {
                        // Путь к изображению товара
                        string imagePath = Path.Combine(projectRoot, "Images", imageName);

                        if (File.Exists(imagePath))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(imagePath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            return bitmap;
                        }
                    }

                    // Заглушка
                    string defaultImagePath = Path.Combine(projectRoot, "Images", "default.png");
                    if (File.Exists(defaultImagePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(defaultImagePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        return bitmap;
                    }

                    // Если даже заглушки нет - возвращаем null
                    return null;
                }
            }

            public string FormattedPrice => Цена.ToString("C", CultureInfo.CurrentCulture);

            public string FormattedDiscountedPrice
            {
                get
                {
                    if (HasDiscount)
                    {
                        decimal discount = GetCurrentDiscount();
                        decimal discountedPrice = Цена * (1 - discount / 100);
                        return discountedPrice.ToString("C", CultureInfo.CurrentCulture);
                    }
                    return string.Empty;
                }
            }

            public string FormattedDiscount => $"{GetCurrentDiscount()}%";

            public bool HasDiscount => GetCurrentDiscount() > 0;

            public bool IsHighDiscount => GetCurrentDiscount() > 15;

            public bool IsOutOfStock => Количество_на_складе == 0;

            public string StockStatus
            {
                get
                {
                    if (Количество_на_складе == 0) return "Нет в наличии";
                    if (Количество_на_складе < 10) return $"Мало: {Количество_на_складе}";
                    return $"В наличии: {Количество_на_складе}";
                }
            }

            public string StockColor
            {
                get
                {
                    if (Количество_на_складе == 0) return "Red";
                    if (Количество_на_складе < 10) return "Orange";
                    return "Green";
                }
            }

            private decimal GetCurrentDiscount()
            {
                // Используем Скидка если есть, иначе Действующая_скидка
                return Скидка ?? Действующая_скидка ?? 0;
            }
        }
    }
}