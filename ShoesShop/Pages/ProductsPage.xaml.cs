using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ShoesShop.Pages
{
    public partial class ProductsPage : Page
    {
        private MainWindow _mainWindow;
        private List<Товары> _allProducts;
        private List<Поставщики> _suppliers;

        // Простые конвертеры
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

                if (role == "Менеджер" || role == "Администратор")
                {
                    ControlPanel.Visibility = Visibility.Visible;
                    LoadSuppliers();
                    SetupFilters();
                }
                else
                {
                    ControlPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ControlPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadProducts()
        {
            try
            {
                using (var context = new Entities())
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
                using (var context = new Entities())
                {
                    _suppliers = context.Поставщики.ToList();
                    SupplierFilterComboBox.Items.Clear();

                    // Добавляем "Все поставщики"
                    SupplierFilterComboBox.Items.Add(new Поставщики { ID = -1, Поставщик = "Все поставщики" });

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
                        filteredProducts = filteredProducts.OrderBy(p => p.Колво_на_складе);
                        break;
                    case 2:
                        filteredProducts = filteredProducts.OrderByDescending(p => p.Колво_на_складе);
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

            // Прокси-свойства для прямого доступа
            public string Наименование_товара => _product.Наименование_товара;
            public string Описание_товара => _product.Описание_товара;
            public decimal Цена => _product.Цена;
            public string Единица_измерения => _product.Единица_измерения;
            public int Колво_на_складе => _product.Колво_на_складе;
            public decimal Действующая_скидка => _product.Действующая_скидка;
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
                        decimal discountedPrice = Цена * (1 - Действующая_скидка / 100);
                        return discountedPrice.ToString("C", CultureInfo.CurrentCulture);
                    }
                    return string.Empty;
                }
            }

            public string FormattedDiscount => $"{Действующая_скидка}%";

            public bool HasDiscount => Действующая_скидка > 0;

            public bool IsHighDiscount => Действующая_скидка > 15;

            public bool IsOutOfStock => Колво_на_складе == 0;

            public string StockStatus
            {
                get
                {
                    if (Колво_на_складе == 0) return "Нет в наличии";
                    if (Колво_на_складе < 10) return $"Мало: {Колво_на_складе}";
                    return $"В наличии: {Колво_на_складе}";
                }
            }

            public string StockColor
            {
                get
                {
                    if (Колво_на_складе == 0) return "Red";
                    if (Колво_на_складе < 10) return "Orange";
                    return "Green";
                }
            }
        }
    }
}