using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace System.Linq.Async;

internal static class AsyncEnumerableGroupExtensions
{
    /// <summary>
    /// Groups sequential elements with the same key
    /// </summary>
    /// <param name="source"></param>
    /// <param name="keySelector"></param>
    /// <param name="comparer"></param>
    /// <typeparam name="TElement"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <returns></returns>
    [LinqTunnel]
    public static IAsyncEnumerable<IAsyncGrouping<TKey, TElement>> GroupSequentialBy<TElement, TKey>(
        this IAsyncEnumerable<TElement> source, 
        Func<TElement, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
    {
        var key = new GroupKey<TKey, TElement>(keySelector, comparer ?? EqualityComparer<TKey>.Default);
        return new GroupedSequentialAsyncEnumerable<TKey, TElement>(source, key);
    }
    
    private class GroupedSequentialAsyncEnumerable<TKey, TElement> : IAsyncEnumerable<IAsyncGrouping<TKey, TElement>>
    {
        private readonly IAsyncEnumerable<TElement> _source;
        private readonly GroupKey<TKey, TElement> _key;

        public GroupedSequentialAsyncEnumerable(IAsyncEnumerable<TElement> source, GroupKey<TKey, TElement> key)
        {
            _source = source;
            _key = key;
        }

        public IAsyncEnumerator<IAsyncGrouping<TKey, TElement>> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            var enumerator = _source.GetAsyncEnumerator(cancellationToken);
            var wrapper = new AsyncGroupedEnumeratorWrapper<TElement>(enumerator, cancellationToken);
            return new GroupedSequentialAsyncEnumerator<TKey, TElement>(wrapper, _key);
        }
    }

    private class GroupedSequentialAsyncEnumerator<TKey, TElement> : IAsyncEnumerator<IAsyncGrouping<TKey, TElement>>
    {
        private readonly AsyncGroupedEnumeratorWrapper<TElement> _source;
        private readonly GroupKey<TKey, TElement> _key;

        public GroupedSequentialAsyncEnumerator(
            AsyncGroupedEnumeratorWrapper<TElement> source,
            GroupKey<TKey, TElement> key)
        {
            _key = key;
            _source = source;
        }
        
        public ValueTask DisposeAsync() => _source.DisposeAsync();
        
        public async ValueTask<bool> MoveNextAsync()
        {
            if (_source.EnumeratorCompleted)
                return false; // Source is exhausted
            
            if (_current == null)
            {
                // Nothing read from source yet
                
                if (!await _source.MoveNextAsync())
                {
                    // Empty source
                    return false;
                }
            }
            else
            {
                // We've done at least one group. Ensure it's completed (i.e. read until the group changes)
                if (!await _current.Source.EnsureGroupCompleted())
                {
                    // Source is exhausted
                    return false;
                }
            }
            
            //Previous group completed or first group started
            _current = new AsyncGroupedSequentialGrouping<TKey, TElement>(_source, _key);
            return true;
        }

        private AsyncGroupedSequentialGrouping<TKey, TElement>? _current;

        public IAsyncGrouping<TKey, TElement> Current => _current ?? throw new InvalidOperationException("Enumeration has not started");
    }

    private class AsyncGroupedSequentialGrouping<TKey, TElement> : IAsyncGrouping<TKey, TElement>
    {
        public readonly AsyncGroupedSequentialGroupEnumerator<TKey, TElement> Source;

        public AsyncGroupedSequentialGrouping(
            AsyncGroupedEnumeratorWrapper<TElement> source, 
            GroupKey<TKey, TElement> key)
        {
            Source = new AsyncGroupedSequentialGroupEnumerator<TKey, TElement>(source, key);
        }

        public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken) => Source;

        public TKey Key => Source.Key;
    }

    private class AsyncGroupedSequentialGroupEnumerator<TKey, TElement> : IAsyncEnumerator<TElement>
    {
        public TKey Key { get; }
        
        private readonly AsyncGroupedEnumeratorWrapper<TElement> _source;
        private readonly GroupKey<TKey, TElement> _groupKey;

        public AsyncGroupedSequentialGroupEnumerator(
            AsyncGroupedEnumeratorWrapper<TElement> source,
            GroupKey<TKey, TElement> groupKey)
        {   
            _source = source;
            _groupKey = groupKey;
            
            Key = groupKey.GetKey(source.Current);
        }

        /// <summary>
        /// Whether the group has been completed
        /// </summary>
        private bool GroupCompleted => !_groupKey.KeyEquals(Key, _source.Current);
        
        /// <summary>
        /// Whether MoveNextAsync has been called yet
        /// </summary>
        private bool _firstItem = true;

        public async ValueTask<bool> MoveNextAsync()
        {
            ThrowIfGroupCompleted();
    
            if (_firstItem)
            {
                _firstItem = false;
                // Value already read, by parent or previous group
                return true;
            }

            if (!await _source.MoveNextAsync())
            {
                // Enumeration has ended
                return false;
            }

            return !GroupCompleted; // If the group has changed, we're done
        }
    
        public TElement Current
        {
            get
            {
                ThrowIfGroupCompleted();
                return _source.Current;
            }
        }
        
        /// <summary>
        /// Throw if the group has been completed
        /// </summary>
        private void ThrowIfGroupCompleted()
        {
            if (GroupCompleted)
                throw new InvalidOperationException("Group Enumeration has ended");
        }

        /// <summary>
        /// In the case where a group was only partially enumerated, this method will complete the group
        /// </summary>
        /// <returns>
        /// True if the group was completed, false if the source was exhausted
        /// </returns>
        public async ValueTask<bool> EnsureGroupCompleted()
        {
            //Loop until the group is completed or the source is exhausted
            while (true)
            {
                if (GroupCompleted)
                    return true; // Group is completed, there is another group
                
                if (!await _source.MoveNextAsync())
                    return false; // Source is exhausted
            }
        }
        
        public ValueTask DisposeAsync() => ValueTask.CompletedTask; //Parent will dispose
    }

    private class GroupKey<TKey, TElement>(Func<TElement, TKey> selector, IEqualityComparer<TKey> comparer)
    {
        public TKey GetKey(TElement element) => selector(element);

        public bool KeyEquals(TKey left, TElement right) => comparer.Equals(left, GetKey(right));
    }

    private class AsyncGroupedEnumeratorWrapper<TElement> : IAsyncDisposable
    {
        private readonly IAsyncEnumerator<TElement> _source;
        private readonly CancellationToken _cancellationToken;

        public AsyncGroupedEnumeratorWrapper(IAsyncEnumerator<TElement> source, CancellationToken cancellationToken)
        {
            _source = source;
            _cancellationToken = cancellationToken;
        }
        
        /// <summary>
        /// Whether the overall enumeration has been completed
        /// </summary>
        public bool EnumeratorCompleted { get; private set; }

        public ValueTask DisposeAsync() => _source.DisposeAsync();

        public async ValueTask<bool> MoveNextAsync()
        {
            ThrowIfCompleted();
            if (await _source.MoveNextAsync(_cancellationToken))
                return true;
            
            // End of source
            EnumeratorCompleted = true;
            return false;
        }

        public TElement Current
        {
            get
            {
                ThrowIfCompleted();
                return _source.Current;
            }
        }
        
        private void ThrowIfCompleted()
        {
            if (EnumeratorCompleted)
                throw new InvalidOperationException("Enumeration has ended");
        }
    }
}