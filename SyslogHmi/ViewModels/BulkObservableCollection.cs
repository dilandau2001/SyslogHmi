using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SyslogHmi.ViewModels
{
    /// <summary>
    /// Una ObservableCollection optimizada que permite añadir o insertar rangos de elementos
    /// notificando a la interfaz de usuario una sola vez al final.
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            // Si la notificación está suprimida, no le avisamos a WPF todavía
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnPropertyChanged(e);
            }
        }

        /// <summary>
        /// Inserta un rango de elementos al principio (Índice 0) de golpe, 
        /// disparando un único evento de refresco visual.
        /// </summary>
        public void PrependRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));

            _suppressNotification = true;
            try
            {
                // Para mantener el orden cronológico invertido (los más nuevos primero),
                // recorremos el lote de atrás hacia adelante al insertar en el índice 0
                var list = new List<T>(collection);
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    Insert(0, list[i]);
                }
            }
            finally
            {
                _suppressNotification = false;
                // Le avisamos a WPF que la lista cambió por completo para que se redibuje
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Elimina un rango de elementos desde el final de la lista de golpe.
        /// </summary>
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
                OnPropertyChanged(new PropertyChangedEventArgs("Count"));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}