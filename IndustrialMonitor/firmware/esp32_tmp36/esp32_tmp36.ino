#include <Arduino.h>

const int PINO_TMP36 = 32;
unsigned long ultimaLeitura = 0;
const unsigned long INTERVALO_LEITURA = 200; // ms

float lerTemperaturaTMP36(int pino) {
    int leituraADC = analogRead(pino);
    float tensao = leituraADC * (3.3f / 4095.0f);
    float temperatura = (tensao - 0.5f) * 100.0f;
    return temperatura;
}

void setup() {
    Serial.begin(115200);
    analogReadResolution(12);
    pinMode(PINO_TMP36, INPUT);
}

void loop() {
    unsigned long tempoAtual = millis();
    
    if (tempoAtual - ultimaLeitura >= INTERVALO_LEITURA) {
        ultimaLeitura = tempoAtual;
        
        float tempC = lerTemperaturaTMP36(PINO_TMP36);
        
        Serial.print("TEMP=");
        Serial.println(tempC, 2);
    }
}