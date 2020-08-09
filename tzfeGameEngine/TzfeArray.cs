using System;
namespace tzfeGameEngine {

    public class TzfeArray<T> {
        private T[] array;

        public TzfeArray(int size) {
            array = new T[size + 1];
        }
        public T get(int index) {
            return array[index];
        }
        public void set(int index, T value) {
            array[index] = value;
        }
    }

}