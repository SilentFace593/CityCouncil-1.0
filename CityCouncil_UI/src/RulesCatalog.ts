export interface RuleSection {
  id: string;
  title: string;
  /** Chaque paragraphe peut contenir des liens internes via la syntaxe [[id|libellé affiché]]. */
  paragraphs: string[];
}

export const RULES_SECTIONS: RuleSection[] = [
  {
    id: "elections",
    title: "Élections de district",
    paragraphs: [
      "Chaque district organise une élection tous les 7 jours in-game. Le nombre de sièges dépend de sa population (1 siège par tranche de 2 000 habitants).",
      "Au 1er tour, si un parti obtient au moins 50% des voix exprimées, il remporte le district directement. Sinon, un second tour est organisé entre les deux partis arrivés en tête, avec report des voix des partis éliminés.",
      "Les sièges sont répartis proportionnellement entre les partis selon la méthode du plus fort reste, en fonction des résultats finaux.",
      "Posséder un district de façon continue peut mener au statut de [[bastion|Bastion]]. Le parti sortant peut aussi bénéficier d'un [[bonus_permanent|Bonus permanent]] s'il enchaîne les victoires à la majorité du conseil municipal.",
    ],
  },
  {
    id: "bastion",
    title: "Bastion",
    paragraphs: [
      "Un parti qui remporte le même district 3 fois de suite en devient le Bastion. Le Bastion accorde un bonus permanent de +4% aux voix de ce parti dans ce district, tant qu'il le conserve.",
      "Le Bastion est perdu immédiatement si un autre parti remporte le district : la série repart alors à 1 pour le nouveau vainqueur.",
      "Un parti détenteur du [[bonus_permanent|Bonus permanent Défensif]] conserve une case de série même en cas de défaite (mais peut tout de même perdre le Bastion si la série tombe à 0).",
      "Un parti détenteur du [[bonus_permanent|Bonus permanent Offensif]] bénéficie d'un bonus de +3% dans tous les Bastions qui ne lui appartiennent pas.",
      "Posséder un Bastion rapporte 1000 points au [[score|Score]] tant qu'il est détenu, et sa perte retire ces mêmes 1000 points.",
    ],
  },
  {
    id: "bonus_permanent",
    title: "Bonus permanents",
    paragraphs: [
      "Un parti qui remporte la majorité du conseil municipal (le plus de sièges toutes couleurs confondues) plusieurs cycles de suite peut se voir attribuer un bonus permanent, à partir de 2 victoires consécutives.",
      "Deux bonus existent : Défensif (protège une case de [[bastion|série de Bastion]] en cas de défaite) et Offensif (+3% dans les Bastions adverses).",
      "Pour les partis IA, le bonus est tiré aléatoirement. Pour le parti du joueur, le choix est proposé directement dans l'onglet Résultats dès que le seuil est atteint.",
      "Remporter la majorité générale rapporte également un trophée définitif de 500 points au [[score|Score]], la première fois que ce parti en prend le contrôle.",
    ],
  },
  {
    id: "financement",
    title: "Financement public",
    paragraphs: [
      "Chaque cycle électoral, une part fixe (définie par le joueur, plafonnée à 100 000 crédits) est répartie à parts égales entre les 5 partis, prélevée sur le trésor municipal.",
      "Une part variable de 1 000 crédits par siège obtenu est versée automatiquement à chaque district qui termine son scrutin.",
      "Les partis perçoivent également des cotisations de leurs adhérents à chaque cycle, proportionnelles à leur nombre de membres.",
      "Cet argent finance la [[propagande|Propagande]], les [[campagnes_district|Campagnes de district]] et les [[sondages|Sondages]].",
    ],
  },
  {
    id: "propagande",
    title: "Propagande (ville entière)",
    paragraphs: [
      "Un parti peut lancer une campagne de propagande ciblant les Adultes ou les Séniors, sur toute la ville, avec 3 intensités (Petite/Moyenne/Forte) offrant un bonus croissant de +2% à +6%.",
      "La campagne dure 7 jours et peut être configurée pour se reconduire automatiquement si les réserves du parti le permettent.",
      "Un seul parti IA sur cinq relance une campagne à chaque cycle selon une probabilité fixe, dans la limite de son budget disponible.",
      "Voir aussi les [[campagnes_district|Campagnes de district]], qui ciblent un district précis plutôt que toute la ville.",
    ],
  },
  {
    id: "campagnes_district",
    title: "Campagnes de district",
    paragraphs: [
      "Contrairement à la propagande ville entière, une campagne de district cible un seul district précis. Un parti peut avoir jusqu'à 3 campagnes de district actives simultanément.",
      "Campagne classique (Boost) : renforce le parti lui-même dans un district qu'il détient déjà, sur 3 paliers d'intensité.",
      "Campagne ciblée propre : inflige -2% à un parti adverse dans ce district, sans aucun risque.",
      "Campagne ciblée sale : inflige -4% à la cible, mais expose le lanceur à un malus aléatoire de 0 à -5% dans ce même district (tiré une seule fois au lancement).",
      "Pour aller plus loin dans l'illégalité, voir les [[commission|Campagnes illégales et Commission Électorale]].",
    ],
  },
  {
    id: "commission",
    title: "Campagnes illégales & Commission Électorale",
    paragraphs: [
      "Une fois une [[caisse_noire|caisse noire]] ouverte, un parti peut financer des campagnes illégales de district, infligeant un malus aléatoire de 0 à 6% à un adversaire précis. Jusqu'à 3 campagnes illégales actives simultanément.",
      "La Commission Électorale surveille chaque parti et effectue un contrôle tous les 7 jours. Plus un parti a de campagnes illégales actives, plus le risque de détection et d'escalade de vigilance est élevé (3 niveaux : Peu vigilant, Sous surveillance, En Alerte).",
      "En l'absence de campagne illégale active, la vigilance redescend automatiquement d'un niveau à chaque contrôle.",
      "En cas de détection : sanction de -10% ville entière pendant 7 jours, amende de 50 000 crédits, et fermeture forcée de la caisse noire avec perte totale du solde qu'elle contenait.",
      "Les IA appliquent les mêmes règles de détection, mais financent leurs campagnes illégales directement depuis leur trésorerie officielle plutôt qu'une caisse noire.",
    ],
  },
  {
    id: "caisse_noire",
    title: "Caisse noire",
    paragraphs: [
      "Le parti du joueur peut ouvrir une caisse noire, un compte séparé de la trésorerie officielle, alimenté par des transferts volontaires depuis le compte principal.",
      "Elle sert exclusivement à financer les campagnes illégales de district (voir [[commission|Commission Électorale]]).",
      "Fermer la caisse noire (volontairement ou suite à une sanction) fait perdre définitivement tout l'argent qu'elle contenait : l'argent ne repart jamais vers le compte principal.",
      "Chaque transfert affiche un libellé de « fausse facture » tiré aléatoirement, purement cosmétique.",
    ],
  },
  {
    id: "sondages",
    title: "Sondages",
    paragraphs: [
      "Le joueur peut commander un sondage d'opinion ville entière pour 4 000 crédits, débités de la trésorerie du parti actif.",
      "Le résultat agrège tous les districts, pondéré par leur nombre de votants, et applique les mêmes effets city-wide qu'une élection réelle (évènements, [[propagande|campagnes de propagande]], sanctions de la [[commission|Commission Électorale]]) — mais ignore les effets purement locaux (Bastion, campagnes de district).",
      "Une marge d'erreur de ±2,5 points est affichée à côté de chaque résultat, pour rester incertain comme un vrai sondage.",
      "Les sondages sont interdits de la veille du 1er tour jusqu'à l'issue du 2e tour, sur toute la ville (inspiré du code électoral français).",
    ],
  },
  {
    id: "score",
    title: "Score",
    paragraphs: [
      "Le score combine deux composantes : les trophées (définitifs, jamais retirés) et la possession actuelle (recalculée en permanence).",
      "Trophées : +150 points à la conquête d'un district (changement de vainqueur), +500 points à la conquête de la majorité générale du conseil municipal (changement de majorité).",
      "Possession : chaque siège détenu vaut 10 points, chaque district actuellement dirigé vaut 300 points, chaque [[bastion|Bastion]] détenu vaut 1000 points — ces points disparaissent si le parti perd l'élément correspondant.",
      "Le score d'un parti repart à zéro (trophées uniquement, la possession se recalcule d'elle-même) si le joueur crée un nouveau parti sur un bord politique déjà utilisé, remplaçant ainsi l'historique de l'ancien parti hôte.",
    ],
  },
];