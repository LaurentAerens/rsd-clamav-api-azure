# JSON payloads for `/scan/json` load tests

Use this folder to store request bodies used by Azure Load Testing/JMeter.

## Naming suggestion
- `clean-*.json` for expected HTTP `200`
- `infected-*.json` for expected HTTP `406`
- `mixed-*.json` for realistic nested payloads

## Included starter payloads
- `clean-small.json`
- `clean-medium.json`
- `clean-nested.json`
- `clean-large.json`
- `infected-eicar-plain.json`
- `infected-eicar-base64.json`
- `mixed-batch-clean.json`
- `mixed-batch-infected.json`

Add your own payloads and then include them in the CSV files under `loadtesting/assets/jmeter/datasets`.
