namespace Laborator3_PSSC.Domain.Models;

// External dependencies of the workflow, injected as functions.
public delegate bool CheckProductExists(ProductCode code);
public delegate int GetStock(ProductCode code);
public delegate bool CheckAddressDeliverable(Address address);
public delegate decimal GetProductPrice(ProductCode code);
