﻿#region License
/* The MIT License (MIT)
 * Copyright (c) 2011 Michael Stum, http://www.Stum.de <opensource@stum.de>
 * 
 * Contributions by
 *   Aaron Navarro - https://github.com/anavarro9731
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;

namespace mstum.utils
{
    /// <summary>
    /// A Circular Buffer holds as many elements as the capacity it was created with. Adding more items overwrites the oldest items.
    /// </summary>
    /// <remarks>
    /// A CircularBuffer{T} can support multiple readers concurrently, as long as the collection is not modified.
    /// Enumerating through a collection is intrinsically not a thread-safe procedure.
    /// In the rare case where an enumeration contends with one or more write accesses, the only way to ensure thread safety is to lock the collection during the entire enumeration.
    /// To allow the collection to be accessed by multiple threads for reading and writing, you must implement your own synchronization.
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class CircularBuffer<T> : ICollection<T>
    {
        /// <summary>
        /// The Index into the _store where the first element if the buffer resides.
        /// </summary>
        private int _start;

        /// <summary>
        /// How many items we currently have (important because _store is bigger than the circular buffer initially)
        /// </summary>
        private int _size;

        /// <summary>
        /// The backing store
        /// </summary>
        private T[] _store;

        /// <summary>
        /// The size of _store
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        /// Incremented with each modification to make sure Enumerators blow up properly
        /// </summary>
        private int _version;

        /// <summary>
        /// Create a new Circular Buffer with the given capacity
        /// </summary>
        /// <param name="capacity"></param>
        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _store = new T[capacity];
        }

        /// <summary>
        /// Since the buffer is infinite and wraps around, use this to calculate an index given a start value and steps
        /// </summary>
        /// <param name="currentIndex"></param>
        /// <param name="steps"></param>
        /// <returns></returns>
        private int CalculateIndex(int currentIndex, int steps)
        {
            if (steps < 0)
            {
                var r = _capacity - (currentIndex - steps) % _capacity;
                if (r == _capacity) r = 0;
                return r;
            }
            else
            {
                return (currentIndex + steps) % _capacity;
            }
        }

        /// <summary>
        /// Add a new item to the buffer. If the buffer is at its capacity, the oldest item will be discarded
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            var index = CalculateIndex(_start, _size);
            _store[index] = item;
            _size++;
            if (_size > _capacity)
            {
                _start = CalculateIndex(_start,1);
                _size = _capacity;
            }
            this._version++;
        }

        /// <summary>
        /// Clear the Buffer, removing all elements
        /// </summary>
        public void Clear()
        {
            if (this._size > 0)
            {
                Array.Clear(this._store, 0, this._capacity);
                this._size = 0;
                this._start = 0;
            }
            this._version++;

        }

        /// <summary>
        /// Does the buffer contain the given item?
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            // It it always safe to start iterating from 0:
            // 1. If the _store isn't full yet, _start will be 0
            // 2. If the _store is full, it doesn't matter if we check elements out of order since we only care about contains

            if (item == null)
            {
                for (int i = 0; i < _size; i++)
                {
                    if (_store[i] == null) return true;
                }
                return false;
            }

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _size; i++)
            {
                if (comparer.Equals(_store[i], item)) return true;
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (_size > (array.Length - arrayIndex))
            {
                throw new ArgumentException("Destination array was not long enough. Check destIndex and length, and the array's lower bounds.", "array");
            }

            if (_start == 0)
            {
                Array.Copy(_store, 0, array, arrayIndex, _size);
            }
            else
            {
                // Need to copy in two chunks. We know that our _store is "full" since _start would be 0 otherwise.
                Array.Copy(_store, _start, array, arrayIndex, _capacity - _start);
                Array.Copy(_store, 0, array, arrayIndex + (_capacity - _start), _start);
            }
        }

        /// <summary>
        /// How many items does this buffer contain?
        /// </summary>
        public int Count
        {
            get { return _size; }
        }

        /// <summary>
        /// Is this buffer read only?
        /// </summary>
        public bool IsReadOnly
        {
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
            get { return false; }
        }

        /// <summary>
        /// Removes an item from the collection. If the item removed occurs at or before the current position the current position is moved back by one.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            if (Contains(item))
            {
                T[] _newStore = new T[_capacity];

                int? skippedAt = null;

                EqualityComparer<T> comparer = EqualityComparer<T>.Default;
                for (int i = 0; i < _size; i++)
                {
                    _newStore[(skippedAt != null ? i - 1 : i)] = _store[i];

                    if (comparer.Equals(_store[i], item))
                    {
                        skippedAt = i;
                        continue;
                    }
                }
                _size--;
                if (_start >= skippedAt && _start > 0)
                {
                    _start--;
                }
                _store = _newStore;
                _version++;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Get an Enumerator to enumerator over the buffer.
        /// </summary>
        /// <remarks>
        /// If the buffer is modified while enumerating, the enumerator throws an exception
        /// </remarks>
        /// <returns></returns>
        public IEnumerator<T> GetEnumerator()
        {
            return new CircularBufferEnumerator(this);
        }

        /// <summary>
        /// Get an Enumerator to enumerator over the buffer.
        /// </summary>
        /// <remarks>
        /// If the buffer is modified while enumerating, the enumerator throws an exception
        /// </remarks>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class CircularBufferEnumerator : IEnumerator<T>
        {
            /// <summary>
            /// The version of the _buffer at the time the enumerator was created
            /// </summary>
            private readonly int _version;

            /// <summary>
            /// The CircularBuffer we are enumerating over
            /// </summary>
            private CircularBuffer<T> _buffer;

            /// <summary>
            /// The Current item
            /// </summary>
            private T _current;

            /// <summary>
            /// How often did we MoveNext?
            /// </summary>
            private int _numSteps;

            /// <summary>
            /// Create an Enumerator for the given buffer
            /// </summary>
            /// <param name="buffer"></param>
            public CircularBufferEnumerator(CircularBuffer<T> buffer)
            {
                _buffer = buffer;
                _version = buffer._version;
                _current = default(T);
            }

            /// <summary>
            /// The Current Item
            /// </summary>
            public T Current
            {
                get { return _current; }
            }

            /// <summary>
            /// The current Item
            /// </summary>
            object System.Collections.IEnumerator.Current
            {
                get
                {
                    if (_numSteps == 0 || _numSteps > _buffer._size)
                    {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return _current;
                }
            }

            /// <summary>
            /// Reset the enumerator to restart enumeration
            /// </summary>
            public void Reset()
            {
                if (_buffer._version != _version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }
                _numSteps = 0;
                _current = default(T);
            }

            /// <summary>
            /// Move the Enumerator forward
            /// </summary>
            /// <returns></returns>
            public bool MoveNext()
            {
                if (_buffer._version != _version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }
                _numSteps++;
                if (_numSteps > _buffer._size)
                {
                    _current = default(T);
                    return false;
                }

                var bufferIndex = _buffer.CalculateIndex(_buffer._start,_numSteps-1);
                _current = _buffer._store[bufferIndex];

                return true;
            }

            public void Dispose()
            {
            }
        }
    }
}
