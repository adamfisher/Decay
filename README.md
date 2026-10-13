# C# `Decay<T>`

[![NuGet](https://img.shields.io/nuget/v/AF.Decay.svg)](https://www.nuget.org/packages/AF.Decay)
[![CI](https://github.com/adamfisher/Decay/actions/workflows/ci.yml/badge.svg)](https://github.com/adamfisher/Decay/actions/workflows/ci.yml)

Wrap any object so its value expires once a condition is met: after a number of accesses, after a point in time, or when your own rule says so.

```
dotnet add package AF.Decay
```

Targets .NET 10 and .NET Standard 2.0.

**Things to Note**
- Policies you don't configure never apply. If you configure several, the first one to be met expires the value.
- Expiration is permanent. Once expired, the wrapped value is released and never comes back.
- Instances are thread-safe, and an access limit is honored exactly under concurrency.
- Every read of `Value`, `TryGetValue` or `GetValueOrDefault` counts as one access. Reading `Expired`, `AccessCount` and similar properties does not.
- `Decay<T>` instances compare by reference, not by the value they wrap.

## Getting Started

Suppose you want `someObject` to expire after it has been accessed five times or after 10 seconds, whichever comes first:

```csharp
var decaying = Decay.Of(someObject)
    .ExpireAfterAccesses(5)
    .ExpireAfter(TimeSpan.FromSeconds(10))
    .Build();

if (decaying.TryGetValue(out var value))
{
    // Still alive: use value.
}
```

After expiry, `Value` returns `default(T)`. To get an exception instead, add `.ThrowOnExpiration()`:

```csharp
var decaying = Decay.Of(someObject).ExpireAfterAccesses(1).ThrowOnExpiration().Build();

_ = decaying.Value; // OK
_ = decaying.Value; // throws ObjectExpiredException; ex.Reason == ExpirationReasons.Count
```

## Reading the Value

| Member | On expiry |
|---|---|
| `TryGetValue(out T value)` | returns `false` |
| `GetValueOrDefault(T fallback)` | returns `fallback` |
| `Value` | returns `default(T)`, or throws if created with `ThrowOnExpiration()` |

Prefer `TryGetValue` when `default(T)` (`0`, `false`, `null`) is a meaningful value.

## Policies

### Expire After Accessing `x` Times

```csharp
var decaying = Decay.OnCount(someObject, 5);
// The first 5 reads return the value; the 6th returns default.
```

### Expire After a Time Horizon

```csharp
var relative = Decay.OnExpiration(someObject, TimeSpan.FromSeconds(10));
var absolute = Decay.OnExpiration(someObject, DateTimeOffset.UtcNow.AddSeconds(10));
```

### Expire On a Custom Condition

The condition receives a `DecayState<T>` holding the value, `AccessCount`, `AccessLimit`, `ExpiresAt` and `Now`, and returns `true` to expire. It runs on every access and every read of `Expired` until the value expires, so keep it fast and free of side effects.

```csharp
var cart = new List<string> { "apple" };
var decaying = Decay.OnCondition(cart, state => state.Value.Count == 0);
```

### Combining Policies

```csharp
var session = Decay.Of(sessionToken)
    .ExpireAfterAccesses(100)
    .ExpireAt(endOfShift)
    .ExpireWhen(state => state.Now.DayOfWeek == DayOfWeek.Sunday)
    .ThrowOnExpiration()
    .Build();
```

A builder also converts implicitly: `Decay<string> session = Decay.Of(sessionToken).ExpireAfterAccesses(100);`

## Inspecting and Controlling

```csharp
bool expired = decaying.Expired;
ExpirationReasons why = decaying.ExpirationReason; // None, Count, Time, Condition, Manual (flags)
long reads = decaying.AccessCount;                 // reads so far
long? limit = decaying.AccessLimit;                // configured limit, or null
long? left = decaying.RemainingAccesses;           // reads left, or null
DateTimeOffset? expiresAt = decaying.ExpiresAt;    // configured expiry, or null
decaying.Expire();                                 // expire right now
```

## Testing Time-Based Expiry

Inject a `TimeProvider`, for example `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`:

```csharp
var time = new FakeTimeProvider();
var decaying = Decay.Of("token").ExpireAfter(TimeSpan.FromMinutes(5)).UseTimeProvider(time).Build();

time.Advance(TimeSpan.FromMinutes(6));
// decaying.Expired == true
```

## Combine with `Lazy<T>`

Delay creating an object and also make it decay. The order in which you nest `Lazy<>` and `Decay<>` determines the behavior.

```csharp
// The expensive object is created on first use; its 10-second lifetime starts then.
var delayedDecaying = new Lazy<Decay<SomeObject>>(() => Decay.OnExpiration(new SomeObject(), TimeSpan.FromSeconds(10)));

// The lifetime starts now, even though SomeObject isn't created yet.
// Useful when an expensive object should never be created if it isn't needed within the window.
var decayingLazy = Decay.OnExpiration(new Lazy<SomeObject>(() => new SomeObject()), TimeSpan.FromSeconds(10));
```

## Collections

Sorting puts expired items first, then items with the fewest remaining accesses, then the earliest expiration time. Custom conditions don't affect order.

```csharp
var list = new List<Decay<int>>();
// ... add some objects
list.Sort(); // Ordered by next to decay
```

Extension methods for collections of `Decay<T>`:

```csharp
// Next item due to decay, removing expired items (if the collection isn't read-only)
var next = list.GetNextDecayingItem();

// Same, but leave expired items in place
var nextKeep = list.GetNextDecayingItem(clearDecayedItems: false);

var expired = list.GetExpired();     // snapshot of expired items
var active = list.GetActive();       // snapshot of active items
int removed = list.RemoveExpired();  // remove expired items, returns count removed
```

## Upgrading from 1.x

2.0 keeps the 1.x API compiling, with these changes:

- **Reference equality.** `==`, `Equals` and `GetHashCode` no longer compare the wrapped values. The 1.x behavior caused collection helpers to remove the wrong item.
- **`default(T)` is a real value.** `new Decay<int>(0)` is no longer born expired.
- **Expiration is permanent**, including when throwing. A custom condition flipping back to `false` no longer revives the value.
- **Access counting** happens on every read, not only when an access limit is set.
- **Sort order** compares remaining accesses, then expiration time. It no longer mixes seconds with access counts.
- **Exception messages** no longer include the wrapped value.
- **Deprecated:** `GetDecayedItems` → `GetExpired`, `GetNonDecayedItems` → `GetActive`, `ClearDecayedItems` → `RemoveExpired`, and the `(DateTimeOffset?, long?)` condition overload → `Decay.OnCondition(value, state => ...)`. These will be removed in 3.0 ([#4](https://github.com/adamfisher/Decay/issues/4)).
