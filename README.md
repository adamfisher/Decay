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
  expirationDateTime: DateTimeOffset.UtcNow.AddSeconds(10), 
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
var decayingObject = new Decay<SomeObject>(myObject, expirationDateTime: DateTimeOffset.UtcNow.AddSeconds(10));
myObject = decayingObject.Value // Value will be null 10 seconds from creation of the Decay<> object.
```

### Expire On Custom Condition

Sometimes you will want to make values decay based on a custom condition. The `expireOnCondition` parameter allows you to define a custom function that should return **true** when the object is considered expired. This function will be called each time `Decay<T>.Value` is accessed to determine if the object should be marked expired. The `expirationTime == null` and `currentCount == 0` (never increments) unless those policies are set when the object is created. 

```csharp
var myObject = new SomeObject();
var decayingObject = new Decay<SomeObject>(myObject, expireOnCondition: (DateTimeOffset? expirationTime, long currentCount) =>
{
    return true; // return true to indicate expiration
});
myObject = decayingObject.Value; // Value will be null and expired when your custom function returns true;
```

### Factory Methods

Instantiate decaying objects with more convenient syntax when you only care about applying a single type of policy:

```csharp
var decayingObject = Decay<string>.OnExpiration("123", TimeSpan.FromMinutes(30));
var decayingObject = Decay<string>.OnCount("123", 10);
var decayingObject = Decay<string>.OnCondition("123", (expirationTime, currentCount) =>
{
	return true; // on expiration
});
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
