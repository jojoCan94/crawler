# Concetti Appresi

Questo documento descrive i concetti principali appresi e implementati nel progetto Google Play Store Crawler.

## HttpClient in C#
- **Utilizzo di HttpClient**: Usato per effettuare richieste HTTP asincrone per raccogliere i dati dal Google Play Store.

## HtmlAgilityPack
- **Parsing HTML**: Utilizzato per analizzare il contenuto HTML delle pagine web e per estrarre informazioni specifiche come URL delle app e metadati.

## Modularizzazione del Codice
- **BufferManager**: Una classe personalizzata per la gestione efficiente dei buffer durante la lettura dei flussi di dati.
- **TimeMeasurement**: Una classe per la misurazione accurata del tempo impiegato dalle operazioni, utilizzando `Stopwatch`.

## Estrazione e Salvataggio dei Dati
- **Estrazione dei Metadati**: Utilizzo di espressioni XPath per estrarre i metadati come titolo, categoria e valutazione delle app.
- **Salvataggio dei Dati**: I metadati estratti vengono salvati in file JSON, uno per ogni app, nella directory `apps`.

## Gestione delle Eccezioni
- **Robustezza del Codice**: Implementazione della gestione delle eccezioni per gestire errori durante le richieste HTTP e il processo di estrazione dei dati.

## Pratiche di Codifica
- **Classe BufferManager**: Gestione della memoria durante la lettura di grandi quantità di dati da flussi.
- **Classe TimeMeasurement**: Utilizzo di un approccio orientato agli oggetti per la misurazione del tempo, favorendo la riusabilità e la manutenibilità del codice.
