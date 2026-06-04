using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// An optimized <see cref="ObservableCollection{T}"/> that allows prepending or removing ranges of items
    /// while notifying the user interface (UI) only once at the very end of the transaction.
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        // Flag used to temporarily mute data binding updates during intense data modifications
        private bool _suppressNotification;

        /// <summary>
        /// Raises the <see cref="ObservableCollection{T}.CollectionChanged"/> event.
        /// </summary>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // If notification is actively suppressed, do not inform WPF/UI frameworks yet
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        /// <summary>
        /// Raises the <see cref="ObservableCollection{T}.PropertyChanged"/> event.
        /// </summary>
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnPropertyChanged(e);
            }
        }

        /// <summary>
        /// Inserts a range of elements at the absolute beginning (Index 0) of the collection all at once,
        /// triggering a single unified visual refresh event.
        /// </summary>
        /// <param name="collection">The batch of items to prepend to the list.</param>
        public void PrependRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            _suppressNotification = true;
            try
            {
                // To maintain reversed chronological order (newest items appearing at the top),
                // we iterate through the incoming batch from back to front while inserting at index 0.
                var list = new List<T>(collection);
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    Insert(0, list[i]);
                }
            }
            finally
            {
                // Unmute notifications inside a finally block to guarantee execution if an error occurs
                _suppressNotification = false;

                // Notify WPF that the list structure has changed completely so it forces a single redraw pass
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Removes a specific quantity of items from the tail end of the collection all at once.
        /// </summary>
        /// <param name="countToRemove">The exact number of old log entries to delete from the bottom.</param>
        public void RemoveFromEnd(int countToRemove)
        {
            if (countToRemove <= 0) return;

            _suppressNotification = true;
            try
            {
                for (var i = 0; i < countToRemove; i++)
                {
                    if (Count > 0)
                    {
                        RemoveAt(Count - 1);
                    }
                }
            }
            finally
            {
                _suppressNotification = false;

                // Signal a total layout reset to clean up trailing elements on screen simultaneously
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}