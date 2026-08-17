using System.Collections.Generic;
using UnityEngine;

public enum Language { English, Spanish }

/// The game's text in both languages.
///
/// English is the source of truth: UI keys carry their English string right here, and a card's
/// English name and rules text live on the Card asset. Spanish is a lookup layered on top, so
/// anything not yet translated falls back to English rather than showing a blank or a raw key.
///
/// GLOSSARY — these terms are used consistently and are the ones worth reviewing first:
///   Block = Bloqueo · Stamina = Vigor · Damage = Daño · Strike = Golpe · Burn = Quemadura
///   Bleed = Sangrado · Divine Shield = Escudo Divino · Scry = Adivinar · Reflex = Reflejo
///   Channel = Canalizar · Spell = Hechizo · Miracle = Milagro · Skill = Habilidad
///   Equipment = Equipo · Stack = Pila · Deck = Mazo
public static class Localization
{
    private const string PrefsKey = "atc.language";

    public static Language Current { get; private set; } = Language.English;

    /// Raised after the language changes so every visible string can redraw itself.
    public static event System.Action Changed;

    private static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        Current = (Language)PlayerPrefs.GetInt(PrefsKey, (int)Language.English);
    }

    public static void Set(Language language)
    {
        loaded = true;
        if (Current == language) return;

        Current = language;
        PlayerPrefs.SetInt(PrefsKey, (int)language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// A UI string. Unknown keys come back as the key itself, which makes a missing entry obvious
    /// on screen rather than silently blank.
    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (!ui.TryGetValue(key, out var forms)) return key;
        return Current == Language.Spanish && !string.IsNullOrEmpty(forms[1]) ? forms[1] : forms[0];
    }

    public static string CardName(Card card)
    {
        if (card == null) return string.Empty;
        return Current == Language.Spanish && cards.TryGetValue(card.cardName, out var forms)
            ? forms[0] : card.cardName;
    }

    public static string CardText(Card card)
    {
        if (card == null) return string.Empty;
        return Current == Language.Spanish && cards.TryGetValue(card.cardName, out var forms)
            ? forms[1] : card.effectDescription;
    }

    #region UI strings  [key] = { English, Spanish }

    private static readonly Dictionary<string, string[]> ui = new Dictionary<string, string[]>
    {
        // Main menu
        ["menu.host"]            = new[] { "CREATE LOBBY", "CREAR PARTIDA" },
        ["menu.join"]            = new[] { "FIND LOBBY", "BUSCAR PARTIDA" },
        ["menu.deck"]            = new[] { "CREATE DECK", "CREAR MAZO" },
        ["menu.settings"]        = new[] { "SETTINGS", "AJUSTES" },
        ["menu.exit"]            = new[] { "EXIT", "SALIR" },
        ["menu.back"]            = new[] { "BACK", "ATRÁS" },
        ["menu.connect"]         = new[] { "CONNECT", "CONECTAR" },
        ["menu.no_deck"]         = new[] { "No deck selected — open Create Deck", "Ningún mazo seleccionado — abre Crear Mazo" },
        ["menu.deck_prefix"]     = new[] { "Deck: ", "Mazo: " },
        ["menu.deck_illegal"]    = new[] { "  (not legal yet)", "  (aún no es válido)" },

        // Settings
        ["settings.audio"]       = new[] { "AUDIO", "AUDIO" },
        ["settings.master"]      = new[] { "Master", "General" },
        ["settings.music"]       = new[] { "Music", "Música" },
        ["settings.sfx"]         = new[] { "SFX", "Efectos" },
        ["settings.language"]    = new[] { "LANGUAGE", "IDIOMA" },
        ["settings.english"]     = new[] { "ENGLISH", "INGLÉS" },
        ["settings.spanish"]     = new[] { "SPANISH", "ESPAÑOL" },

        // Join
        ["join.title"]           = new[] { "JOIN A MATCH", "UNIRSE A UNA PARTIDA" },
        ["join.address_hint"]    = new[] { "Host address (shown on the host's screen)", "Dirección del anfitrión (aparece en su pantalla)" },

        // Deck panel
        ["deck.title"]           = new[] { "CHOOSE YOUR DECK", "ELIGE TU MAZO" },
        ["deck.use"]             = new[] { "USE THIS DECK", "USAR ESTE MAZO" },
        ["deck.build"]           = new[] { "BUILD A DECK", "CONSTRUIR UN MAZO" },
        ["deck.none"]            = new[] { "(no deck)", "(sin mazo)" },
        ["deck.cards_word"]      = new[] { "cards", "cartas" },
        ["deck.needs"]           = new[] { "needs", "necesita" },
        ["deck.no_attributes"]   = new[] { "no attributes", "sin atributos" },

        // Deck builder
        ["builder.all_cards"]    = new[] { "ALL CARDS", "TODAS LAS CARTAS" },
        ["builder.your_deck"]    = new[] { "YOUR DECK", "TU MAZO" },
        ["builder.filter_all"]   = new[] { "ALL", "TODAS" },
        ["builder.save"]         = new[] { "SAVE DECK", "GUARDAR MAZO" },
        ["builder.points"]       = new[] { "points", "puntos" },
        ["builder.legal"]        = new[] { "Deck is legal.", "El mazo es válido." },
        ["builder.copy_limit"]   = new[] { "already at the copy limit.", "ya alcanzó el límite de copias." },
        ["builder.no_points"]    = new[] { "Not enough attribute points.", "No hay suficientes puntos de atributo." },
        ["builder.saved"]        = new[] { "Saved", "Guardado" },
        ["builder.default_name"] = new[] { "My Deck", "Mi Mazo" },

        // Targeting prompts
        ["prompt.default"]       = new[] { "Choose a target  —  Esc to cancel", "Elige un objetivo  —  Esc para cancelar" },
        ["prompt.destroy_card"]  = new[] { "Choose an enemy card in play to destroy", "Elige una carta enemiga en juego para destruir" },
        ["prompt.destroy_equip"] = new[] { "Choose an enemy equipment to destroy", "Elige un equipo enemigo para destruir" },
        ["prompt.return_card"]   = new[] { "Choose an enemy card in play to return to its owner's hand", "Elige una carta enemiga en juego para devolver a la mano de su dueño" },
        ["prompt.return_equip"]  = new[] { "Choose an enemy equipment to return to its owner's hand", "Elige un equipo enemigo para devolver a la mano de su dueño" },
        ["prompt.destroy_cond"]  = new[] { "Choose a condition to destroy", "Elige una condición para destruir" },
        ["prompt.recall"]        = new[] { "Choose a Skill or Spell in your discard to take back", "Elige una Habilidad o Hechizo de tu descarte para recuperar" },
        ["prompt.cast_discard"]  = new[] { "Choose a Spell in your discard to cast", "Elige un Hechizo de tu descarte para lanzar" },
        ["prompt.stack_destroy"] = new[] { "Choose a card on the stack to destroy", "Elige una carta en la pila para destruir" },
        ["prompt.stack_return"]  = new[] { "Choose a card on the stack to return to its owner's hand", "Elige una carta en la pila para devolver a la mano de su dueño" },
        ["prompt.own_equip"]     = new[] { "Choose one of your equipment to take back", "Elige uno de tus equipos para recuperar" },
        ["prompt.copy_equip"]    = new[] { "Choose an enemy equipment to copy", "Elige un equipo enemigo para copiar" },
        ["prompt.set_aside"]     = new[] { "Choose a card to set aside for next turn", "Elige una carta para reservar para el próximo turno" },
        ["prompt.pickpocket"]    = new[] { "Choose a card to take from your opponent's hand", "Elige una carta para robar de la mano de tu oponente" },
        ["prompt.strike"]        = new[] { "Choose one of your weapons to Strike with — it untaps and attacks now", "Elige un arma para Golpear — se endereza y ataca ahora" },
        ["prompt.discard"]       = new[] { "Choose a card to discard", "Elige una carta para descartar" },
        ["prompt.replace"]       = new[] { "is full — choose the card this one replaces", "está lleno — elige la carta que se reemplaza" },
    };

    #endregion

    #region Card text  [English name] = { Spanish name, Spanish rules text }

    private static readonly Dictionary<string, string[]> cards = new Dictionary<string, string[]>
    {
        // Warrior
        ["Brace"]               = new[] { "Prepararse", "Gana 4 Bloqueo." },
        ["Light Swing"]         = new[] { "Tajo Ligero", "Golpe" },
        ["Kite Shield"]         = new[] { "Escudo de Cometa", "Activar (Reflejo) — Gana 2 Bloqueo" },
        ["Heavy Swing"]         = new[] { "Tajo Pesado", "Golpe: +2 Daño" },
        ["Second Wind"]         = new[] { "Segundo Aliento", "Gana 2 Vigor." },
        ["Iron Skin"]           = new[] { "Piel de Hierro", "Inicio del turno: Gana 1 Bloqueo" },
        ["Hurl"]                = new[] { "Lanzamiento", "Golpe: Inflige el doble de daño. Destruye esta arma." },
        ["Sunder"]              = new[] { "Escindir", "Destruye el equipo objetivo." },
        ["Unrelenting Rage"]    = new[] { "Furia Implacable", "Tus cartas de Golpe cuestan 1 Vigor menos (mínimo 0)." },
        ["Layered Armour"]      = new[] { "Armadura en Capas", "Inicio del turno: Pierde 1 Vigor. Puedes equipar 1 Armadura más." },
        ["Tower Shield"]        = new[] { "Escudo de Torre", "Activar (Reflejo) — Gana 3 Bloqueo" },
        ["Greatclub"]           = new[] { "Gran Garrote", "Activar (Canalizar) — Inflige 4 de daño." },
        ["Iron Plate"]          = new[] { "Placa de Hierro", "Inicio del turno: Gana 3 Bloqueo" },
        ["Broken Stance"]       = new[] { "Postura Rota", "Cada vez que ganes Bloqueo, redúcelo en 2." },
        ["Monolith"]            = new[] { "Monolito", "Activar (Canalizar) Paga 1 Vigor — Inflige 7 de daño." },
        ["Skull Splitter"]      = new[] { "Rompecráneos", "Golpe: El oponente descarta 3 cartas." },
        ["Fracture"]            = new[] { "Fractura", "Inicio del turno: Pierde 1 Vigor." },
        ["Earthquake"]          = new[] { "Terremoto", "Destruye todo el equipo del oponente." },

        // Rogue
        ["Flow State"]          = new[] { "Estado de Flujo", "Tu próxima carta se juega a velocidad de Reflejo. Adivina 1." },
        ["Dagger"]              = new[] { "Daga", "Activar (Canalizar) — Inflige 1 de daño." },
        ["Evasive Step"]        = new[] { "Paso Evasivo", "Evita la próxima fuente de daño directo este turno." },
        ["Quick Jab"]           = new[] { "Golpe Rápido", "Inflige 3 de daño." },
        ["Open Veins"]          = new[] { "Venas Abiertas", "Golpe: -1 Daño. Aplica 1 Sangrado." },
        ["Pickpocket"]          = new[] { "Carterista", "Mira la mano de tu oponente y roba una carta." },
        ["Hidden Dagger"]       = new[] { "Daga Oculta", "Activar (Reflejo) — Inflige 1 de daño. +2 si jugaste una carta de Reflejo este turno." },
        ["Backstab"]            = new[] { "Puñalada por la Espalda", "Golpe: Reflejo" },
        ["Slingshot"]           = new[] { "Honda", "Activar (Canalizar) Descarta 1 Equipo o quita uno del descarte — Inflige 3 de daño." },
        ["Disarm"]              = new[] { "Desarmar", "Devuelve el equipo objetivo a la mano del oponente." },
        ["Bait and Switch"]     = new[] { "Cambiazo", "Devuelve a tu mano un Equipo que controles. Puedes jugar 1 Equipo de tu mano sin pagar su coste." },
        ["Keen Instinct"]       = new[] { "Instinto Agudo", "Todas tus cartas pueden jugarse a velocidad de Reflejo." },
        ["Dual Wielding"]       = new[] { "Doble Empuñadura", "Inicio del turno: Pierde 1 Vigor. Puedes equipar 1 arma más." },
        ["Double Strike"]       = new[] { "Golpe Doble", "Golpe. Golpe." },
        ["Sleight of Hand"]     = new[] { "Juego de Manos", "Copia un equipo del oponente y destruye el original." },
        ["Muscle Memory"]       = new[] { "Memoria Muscular", "Tus habilidades cuestan 1 menos." },
        ["Set-Up"]              = new[] { "Preparación", "Coloca una carta boca abajo; el próximo turno juégala con coste 0." },
        ["Dagger Dance"]        = new[] { "Danza de Dagas", "Durante el resto del turno, tus habilidades tienen: Golpe." },
        ["Light Speed"]         = new[] { "Velocidad de la Luz", "Toma un turno adicional después de este." },
        ["Defensive Stance"]    = new[] { "Postura Defensiva", "Ganas +1 Bloqueo de todas las fuentes." },

        // Mage
        ["Dart"]                = new[] { "Dardo", "Inflige 2 de daño." },
        ["Arcane Bolt"]         = new[] { "Rayo Arcano", "Adivina 1. Inflige 3 de daño." },
        ["Arcane Barrier"]      = new[] { "Barrera Arcana", "Gana X + 1 Bloqueo, donde X es el número de Hechizos en tu descarte." },
        ["Study"]               = new[] { "Estudio", "Roba 3 cartas." },
        ["Fire Bolt"]           = new[] { "Rayo de Fuego", "Inflige 2 de daño. Aplica 2 Quemadura." },
        ["Recall"]              = new[] { "Rememorar", "Devuelve a tu mano una Habilidad o Hechizo objetivo de tu descarte." },
        ["Divination"]          = new[] { "Adivinación", "Roba 2 cartas." },
        ["Cloak of Protection"] = new[] { "Manto de Protección", "Inicio de tu turno: gana 1 Bloqueo. Cada vez que juegues un Hechizo, gana 1 Bloqueo." },
        ["Arcane Staff"]        = new[] { "Bastón Arcano", "Activar (Reflejo) — inflige X + 1 de daño, donde X es el número de Hechizos que jugaste este turno." },
        ["Rewind"]              = new[] { "Rebobinar", "Devuelve la carta objetivo de la pila a la mano de su dueño." },
        ["Scrying Orb"]         = new[] { "Orbe Vidente", "Inicio de tu turno: Adivina 2." },
        ["Aether Focus"]        = new[] { "Foco de Éter", "Inicio de tu turno: gana 1 Vigor." },
        ["Spellbound Grimoire"] = new[] { "Grimorio Encantado", "Activar (Canalizar) — roba 1 carta." },
        ["Spellbook"]           = new[] { "Libro de Hechizos", "Activar (Canalizar) — lanza un Hechizo objetivo de tu descarte. Después se retira del juego." },
        ["Leyline"]             = new[] { "Línea Ley", "Tus Hechizos cuestan 1 menos." },
        ["Mind Over Matter"]    = new[] { "Mente sobre Materia", "Cada vez que juegues un Hechizo, Adivina 1." },
        ["Omniscience"]         = new[] { "Omnisciencia", "Roba tu mazo." },
        ["Fireball"]            = new[] { "Bola de Fuego", "Inflige 6 de daño. Aplica 3 Quemadura." },
        ["Timewatch"]           = new[] { "Reloj del Tiempo", "Activar (Reflejo) — reordena la pila." },
        ["Meteor Strike"]       = new[] { "Impacto de Meteoro", "Inflige 21 de daño." },

        // Cleric
        ["Heal"]                = new[] { "Curar", "Restaura 3 de Vida." },
        ["Holy Fire"]           = new[] { "Fuego Sagrado", "Aplica 3 Quemadura." },
        ["Aegis"]               = new[] { "Égida", "Gana 4 de Escudo Divino." },
        ["Absolution"]          = new[] { "Absolución", "Elimina todos los penalizadores de ti mismo. Roba 1 carta." },
        ["Confession"]          = new[] { "Confesión", "Mira la mano de tu oponente. Adivina 1." },
        ["Tithe"]               = new[] { "Diezmo", "La próxima carta que juegue tu oponente este turno cuesta 1 más." },
        ["Purifying Flame"]     = new[] { "Llama Purificadora", "Pierde toda la Quemadura y cúrate esa cantidad." },
        ["Silence"]             = new[] { "Silencio", "Tu oponente no puede jugar cartas de Reflejo durante el resto del turno." },
        ["Sacred Relic"]        = new[] { "Reliquia Sagrada", "Activar (Canalizar) — gana 2 de Escudo Divino." },
        ["Vow of Penance"]      = new[] { "Voto de Penitencia", "La próxima vez que un jugador juegue más de una carta en un turno, recibe 3 de daño." },
        ["Guilt"]               = new[] { "Culpa", "Cada vez que robes una carta, pierdes 1 de Vida." },
        ["Sacred Interdict"]    = new[] { "Interdicto Sagrado", "Destruye la carta objetivo de la pila." },
        ["Bless"]               = new[] { "Bendición", "Gana 2 Vigor al inicio de tu próximo turno." },
        ["Immolate"]            = new[] { "Inmolar", "Aplica 6 Quemadura. Ganas 3 Quemadura." },
        ["Purify"]              = new[] { "Purificar", "Destruye la condición objetivo." },
        ["Tax"]                 = new[] { "Impuesto", "Todos los costes aumentan en 1." },
        ["Sacred Tome"]         = new[] { "Tomo Sagrado", "Activar (Canalizar) — lanza un Hechizo objetivo de tu descarte. Después se retira del juego." },
        ["Sacred Ground"]       = new[] { "Tierra Sagrada", "Tus Milagros cuestan 1 menos." },
        ["Divine Intervention"] = new[] { "Intervención Divina", "Durante el resto del turno tus costes son 0 y no puedes robar cartas." },
        ["Ascension"]           = new[] { "Ascensión", "Si tu mazo y tu mano están vacíos, ganas la partida." },
    };

    #endregion
}
