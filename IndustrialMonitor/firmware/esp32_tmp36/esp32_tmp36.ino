#include <Arduino.h>

const int PINO_TMP36 = 32;
unsigned long ultimaLeitura = 0;
const unsigned long INTERVALO_LEITURA = 200; // ms

float lerTemperaturaTMP36(int pino) {
    int leituraADC = analogRead(pino);
    // Conversão de 12 bits (0 a 4095) com tensão de referência de 3.3V
    float tensao = leituraADC * (3.3f / 4095.0f);
    // Offset de 0.5V (500mV) a 0°C e fator de escala de 10mV/°C (100°C/V)
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
    
    // Timer não-bloqueante mantendo o loop livre para futuras integrações (ex: motores)
    if (tempoAtual - ultimaLeitura >= INTERVALO_LEITURA) {
        ultimaLeitura = tempoAtual;
        
        float tempC = lerTemperaturaTMP36(PINO_TMP36);
        
        Serial.print("TEMP=");
        Serial.println(tempC, 2);
    }
}