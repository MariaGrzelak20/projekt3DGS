using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class realSHFunctions : MonoBehaviour
{
    
    public float[] realSHCoeffcients() 
    {
        //                      [0] [1]     [2] [3] [4]     [5]     [6] [7] [8] 
        //ponumerowane: aml:    a00 a(-1)1  a01 a11 a(-2)2  a(-1)2  a02 a12 a22 
        float[] coefficients = new float[9];

        //dla WSZSYTKICH pozycji kamer musimy oblczyc je

        return coefficients;
    }

    public float baseRealSH(float l, float m, float theta, float phi) 
    {
        float ylm = 0;
        
        if (m > 0) 
        {
            ylm =   Mathf.Sqrt(2)*alphalm(l,m) * 
                    legandrePolynomial(l,m, Mathf.Cos(theta))* 
                    Mathf.Cos(m*phi);
        }

        if(m==0) 
        {
            ylm =   alphalm(l, 0) * 
                    legandrePolynomial(l, 0, Mathf.Cos(theta));
        }

        if (m < 0) 
        {
            ylm =   Mathf.Sqrt(2) *
                    alphalm(l, Mathf.Abs(m)) *
                    legandrePolynomial(l, Mathf.Abs(m), Mathf.Cos(theta)) *
                    Mathf.Sin(phi * Mathf.Abs(m));
        }

        return ylm;
    }

    //stowarzyszony wielomian legandre'a, hardkode dla l=2, poniewa¿ powinno to wystarczyæ, dla wy¿szego l potrzebna funkcja rekurencyjna
    public float legandrePolynomial(float l, float m, float x) {

        float wynik = 1;

        if (l == 0) 
        {
            wynik = 1;
        }
        if (l == 1) 
        {
            if (m == 0) 
            {
                wynik = x;
            }
            if (m == 1) 
            {
                wynik = -1 * Mathf.Pow((1-x), 1/2);                             //-(1-x^2) ^(1/2)
            }
        }
        if (l == 2) 
        {
            if (m == 0) 
            {
                wynik = (1 / 2) * (3 * x - 1);                                  //(1/2)*(3(x^2)-1)
            }
            if (m == 1) 
            {
                wynik = -3 * x * Mathf.Pow(1 - Mathf.Pow(x, 2), (1 / 2));       //-3x(1-x^2)^(1/2)
            }
            if (m == 2) 
            {
                wynik = 3 * (1 - Mathf.Pow(x, 2));
            }
        }

        return wynik; 
    }

    private float silnia(float a) 
    {
        if (a < 1)
            return 1;
        else
            return a * silnia(a - 1);
    }

    //czynnik normalizacyjny
    private float alphalm(float l, float m) 
    {
        return Mathf.Sqrt(
            ((2 * l + 1) / 4 * Mathf.PI) *          
            (silnia(l - m) / silnia(l + m))       
            );
    }
}
