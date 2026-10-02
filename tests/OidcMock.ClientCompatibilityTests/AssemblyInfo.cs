using Xunit;

// El log del host de pruebas es un almacen global compartido (CollectingLoggerProvider), y ademas
// cada test levanta dos Kestrel en un puerto reservado. Con las pruebas en paralelo, un volcado
// diagnostico mezclaria el log de otro test y los puertos se repetirian. Sin paralelismo, el fallo que
// se ve es el del mock y no el del arnes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]