using System;
// este delegate e' a base para o event Move do slider
delegate void MoveEventHandler(object source, MoveEventArgs e);

// esta  classe contem os argumentos do evento move do slider
public class MoveEventArgs : EventArgs
{
    public int position;
    public bool valid;
}


class Slider
{
    private int position;
    public event MoveEventHandler E;
    public int Position
    {
        get
        {
            return position;
        }
        // e' este bloco que e' executado quando se move o slider
        set
        {
            MoveEventArgs args = new MoveEventArgs();
            args.position = value;
            E(this, args);
            if (args.valid == true)
            {
                position = value;
                Console.WriteLine("Slides set position to {0}", value);
            }
            else
            {
                Console.WriteLine("Slide set position '{0}' is invalid", value);
            }
        }
    }
}


class Form
{
    static void Main()
    {
        Slider slider = new Slider();

        // TODO: register with the Move event
        slider.E += new MoveEventHandler(slider_Move);

        // estas sao as duas alteracoes simuladas no slider
        slider.Position = 20;
        slider.Position = 60;
        Console.Read();
    }

    // este é o método que deve ser chamado quando o slider e' movido
    static void slider_Move(object source, MoveEventArgs e)
    {
        if (e.position <= 50)
            e.valid = true;
    }
}