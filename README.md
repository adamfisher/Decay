# C# `Decay<T>`

[![](https://raw.githubusercontent.com/pixel-cookers/built-with-badges/master/nuget/nuget-long.png)](https://www.nuget.org/packages/AF.Decay)

Decay any object causing it's value to expire after a specific condition becomes true.

**Things to Note**
- Conditions left unspecified in the constructor of a `Decay<>` object will not be applied toward the expiration policy of the decaying object.
- `ObjectDecayedException` will be thrown when the value of the decaying object expires if you set `throwExceptionOnExpiration` to `true`. You can inspect the key of the `Data` dictionary of the exception to determine which policies caused the object to expire.

## Getting Started

Suppose you have an instance called `SomeObject` and you want it to expire after acessing it more than five times or if it has been more than 10 seconds, whichever comes first.

```csharp
var myObject = new SomeObject();

var decayingObject = new Decay<SomeObject>(myObject, 
  expireAfterCount: 5, 
  expireAfterTime: TimeSpan.FromSeconds(10), 
  throwExceptionOnExpiration: true);

myObject = decayingObject.Value; // Access the value up to 5 times within the next 10 seconds before it decays.

// 10 seconds later...

myObject = decayingObject.Value; // myObject == null
```

### Expire After Accessing `x` Times

```csharp
var myObject = new SomeObject();
var decayingObject = new Decay<SomeObject>(myObject, expireAfterCount: 5);
myObject = decayingObject.Value // Value will be null after accessing it five times.
```

### Expire After Time Horizon

```csharp
var myObject = new SomeObject();
var decayingObject = new Decay<SomeObject>(myObject, expireAfterTime: TimeSpan.FromSeconds(10));
myObject = decayingObject.Value // Value will be null 10 seconds from creation of the Decay<> object.
```

### Expire On Custom Condition

Sometimes you will want to make values decay based on a custom condition. The `expireOnCondition` parameter allows you to define a custom function that should return **true** when the object is considered expired.

```csharp
var myObject = new SomeObject();
var decayingObject = new Decay<string>(value, expireOnCondition: ((DateTimeOffset creationTime, long currentAccessCount, TimeSpan expireAfterTime) =>
{
    // Determine when this object will expire with custom logic.
    return true;
}));
myObject = decayingObject.Value; // Value will be null and expired when your custom function returns true;
```

### Combine with `Lazy<T>`

Perhaps you want to delay the creation of the object but also cause it to decay. Depending on how you wrap `Lazy<>` and `Decay<>` will determine their behavior.

```csharp
// Delay creation of an expensive object that will expire 10 seconds after it was created.
var delayedDecayingObject = new Lazy<Decay<SomeObject>>(() => new Decay<SomeObject>(new SomeObject(), expireAfterTime: TimeSpan.FromSeconds(10)));

// The entire Lazy<> object has started to decay even though the internal SomeObject value has not been initialized yet.
// This could be useful when it's possible an expensive object should never be created if it's not used within the decaying expiration policy.
var decayingObject = new Decay<Lazy<SomeObject>>(new Lazy<SomeObject>(() => new SomeObject()), expireAfterTime: TimeSpan.FromSeconds(10));
```

### Sort

You can also sort a list of decaying objects into the order in which they are next to decay:

```csharp
var list = new List<Decay<int>>();
// ... add some objects
list.Sort(); // Ordered by next to decay
```
