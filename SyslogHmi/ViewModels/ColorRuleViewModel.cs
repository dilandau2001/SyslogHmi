using SyslogHmi.Models;
using SyslogHmi.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// ViewModel responsible for managing the collection of color rules, 
    /// handling operations like adding, deleting, and reordering rules by priority.
    /// </summary>
    public class ColorRuleViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// Gets the observable collection of color rules displayed in the UI.
        /// </summary>
        public ObservableCollection<ColorRule> ColorRules { get; }

        /// <summary>
        /// Gets the nested ViewModel responsible for the rule creation and edition form.
        /// </summary>
        public ColorRuleFormViewModel FormViewModel { get; }

        /// <summary>
        /// Gets or sets the currently selected rule in the workspace list.
        /// Triggering selection automatically populates the form for editing.
        /// </summary>
        public ColorRule SelectedRule
        {
            get;
            set
            {
                SetProperty(ref field, value);

                // If a rule is selected, pass its data to the form for modification
                if (value != null)
                {
                    FormViewModel.LoadRuleForEditing(value);
                }
            }
        }

        /// <summary>
        /// Command to permanently delete the selected rule from both UI and database.
        /// </summary>
        public ICommand DeleteRuleCommand { get; }

        /// <summary>
        /// Command to move the selected rule up in priority (closer to the top).
        /// </summary>
        public ICommand MoveRuleUpCommand { get; }

        /// <summary>
        /// Command to move the selected rule down in priority (closer to the bottom).
        /// </summary>
        public ICommand MoveRuleDownCommand { get; }

        /// <summary>
        /// Command to clear selection and reset the form to create a brand new rule.
        /// </summary>
        public ICommand NewRuleCommand { get; }

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler RuleChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorRuleViewModel"/> class.
        /// </summary>
        /// <param name="databaseService">The infrastructure service used to persist rules data.</param>
        public ColorRuleViewModel(DatabaseService databaseService = null)
        {
            ColorRules = new ObservableCollection<ColorRule>();
            FormViewModel = new ColorRuleFormViewModel();
            _databaseService = databaseService;

            // Command wiring with execution predicates (CanExecute)
            DeleteRuleCommand = new RelayCommand(_ => DeleteRule(), _ => SelectedRule != null);
            MoveRuleUpCommand = new RelayCommand(_ => MoveRuleUp(), _ => CanMoveRuleUp());
            MoveRuleDownCommand = new RelayCommand(_ => MoveRuleDown(), _ => CanMoveRuleDown());
            NewRuleCommand = new RelayCommand(_ => NewRule());

            // Initialize the list by fetching stored configuration data
            LoadRulesFromDatabase();

            // Subscribe to the form persistence events
            // Handle rule saved from form
            FormViewModel.RuleSaved += (sender, rule) =>
            {
                if (FormViewModel.IsEditMode)
                {
                    int index = -1;

                    // 1. ESTRATEGIA PRINCIPAL: Como SelectedRule apunta a la regla vieja que está 
                    // actualmente seleccionada en el ListBox, le pedimos su índice directo.
                    if (SelectedRule != null)
                    {
                        index = ColorRules.IndexOf(SelectedRule);
                    }

                    // 2. ESTRATEGIA DE RESPALDO: Si por alguna razón visual se deseleccionó, 
                    // buscamos por la prioridad única que conservamos en el método SaveRule().
                    if (index == -1)
                    {
                        var existingByPriority = ColorRules.FirstOrDefault(r => r.Priority == rule.Priority);
                        if (existingByPriority != null)
                        {
                            index = ColorRules.IndexOf(existingByPriority);
                        }
                    }

                    // 3. ESTRATEGIA POR ID (Por si las moscas)
                    if (index == -1 && rule.Id > 0)
                    {
                        var existingById = ColorRules.FirstOrDefault(r => r.Id == rule.Id);
                        if (existingById != null)
                        {
                            index = ColorRules.IndexOf(existingById);
                        }
                    }

                    // Persist the changes into the database
                    if (_databaseService != null)
                    {
                        _databaseService.SaveColorRule(rule);
                    }

                    // Si encontramos la posición de la regla vieja, la intercambiamos por la nueva
                    if (index != -1)
                    {
                        ColorRules[index] = rule;
                    }

                    UpdatePriorities();
                    SelectedRule = null; // Clear selection to clean up the form layout
                }
                else
                {
                    // New rule flow (remains exactly the same)
                    rule.Priority = ColorRules.Count;
                    ColorRules.Add(rule);

                    if (_databaseService != null)
                    {
                        _databaseService.SaveColorRule(rule);
                    }
                }

                RuleChanged?.Invoke(this, EventArgs.Empty);
            };

            FormViewModel.Cancelled += (sender, args) =>
            {
                SelectedRule = null;
            };
        }

        /// <summary>
        /// Fetches all color rules configured in the database layer and populates the reactive collection.
        /// </summary>
        private void LoadRulesFromDatabase()
        {
            if (_databaseService == null) return;

            try
            {
                var rulesFromDb = _databaseService.GetAllColorRules();
                ColorRules.Clear();
                foreach (var rule in rulesFromDb)
                {
                    ColorRules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading color rules from database: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the currently selected rule from both data storage and UI collections.
        /// </summary>
        private void DeleteRule()
        {
            if (SelectedRule == null) return;

            // Permanently remove record from relational persistence storage
            if (SelectedRule.Id > 0 && _databaseService != null)
            {
                try
                {
                    _databaseService.DeleteColorRule(SelectedRule.Id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting rule from database: {ex.Message}");
                }
            }

            ColorRules.Remove(SelectedRule);
            SelectedRule = null;
        }

        /// <summary>
        /// Resets state parameters to allow the input form to receive input details for a new rule.
        /// </summary>
        private void NewRule()
        {
            var rule = new ColorRule();
            SelectedRule = rule;
            ColorRules.Add(rule); // Add to collection to allow form binding and immediate editing
            FormViewModel.Reset();
        }

        /// <summary>
        /// Moves the selected rule one step higher in the collection stack, increasing its evaluation priority.
        /// </summary>
        private void MoveRuleUp()
        {
            if (SelectedRule == null || ColorRules.IndexOf(SelectedRule) == 0) return;

            var index = ColorRules.IndexOf(SelectedRule);
            ColorRules.Move(index, index - 1);
            UpdatePriorities();
        }

        /// <summary>
        /// Moves the selected rule one step lower in the collection stack, decreasing its evaluation priority.
        /// </summary>
        private void MoveRuleDown()
        {
            if (SelectedRule == null || ColorRules.IndexOf(SelectedRule) == ColorRules.Count - 1) return;

            var index = ColorRules.IndexOf(SelectedRule);
            ColorRules.Move(index, index + 1);
            UpdatePriorities();
        }

        /// <summary>
        /// Verifies whether the rule can be shifted up (e.g., it is not already the top-priority rule).
        /// </summary>
        private bool CanMoveRuleUp()
        {
            return SelectedRule != null && ColorRules.IndexOf(SelectedRule) > 0;
        }

        /// <summary>
        /// Verifies whether the rule can be shifted down (e.g., it is not already the absolute last rule).
        /// </summary>
        private bool CanMoveRuleDown()
        {
            return SelectedRule != null && ColorRules.IndexOf(SelectedRule) < ColorRules.Count - 1;
        }

        /// <summary>
        /// Synchronizes the collection indexes with the internal database priority sequence values.
        /// </summary>
        private void UpdatePriorities()
        {
            for (int i = 0; i < ColorRules.Count; i++)
            {
                ColorRules[i].Priority = i;
            }
        }

        /// <summary>
        /// Evaluates a syslog target message against the collection to find the first matching rule based on priority order.
        /// </summary>
        /// <param name="message">The incoming syslog message model to test.</param>
        /// <returns>The first applicable <see cref="ColorRule"/> configuration, or null if no patterns match.</returns>
        public ColorRule GetApplicableRule(SyslogMessage message)
        {
            // Rules are ordered by priority sequence index (top items take evaluation precedence)
            return ColorRules
                .Where(r => r.Matches(message))
                .FirstOrDefault();
        }
    }
}