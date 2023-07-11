// State tracking the current trip configuration.
const maxDays = 15;

var trip;

var dayRoutes = [];
var visibleMapSegments = {};
var highlightedDays = 0;


function CreateDefaultTrip()
{
  trip = {
    Duration: 8,

    StartTrailhead: 'Longmire',
    EndTrailhead: 'Longmire',

    SelectedCampsites: [
      'South Puyallup River',
      'Golden Lakes',
      'Mowich Lake',
      'Dick Creek',
      'Sunrise Camp',
      'Summerland',
      'Nickel Creek'
    ],

    Via: [],
    ViaSprayPark: false,

    UseGroupSites: false
  };
}


function PopulateUnusedCampsites()
{
  var defaultCampsite = trip.UseGroupSites ? 'Devil\'s Dream' : 'Pyramid Creek';

  while (trip.SelectedCampsites.length < maxDays - 1) {
    trip.SelectedCampsites.push(defaultCampsite);
  }
}


// Static data about available options to be displayed in the UI.
const placeNames = {
  'Wonderland Trailheads': [
    'Longmire',
    'Mowich Lake',
    'Sunrise',
    'White River',
    'Box Canyon'
  ],

  'Alternative Accesses': [
    'Fryingpan Creek',
    'Reflection Lakes'
  ],

  'Eastside Trailheads': [
    'G. of Patriarchs trhd',
    'Upper Owyhigh trhd',
    'Deer Creek trhd',
    'Tipsoo Lake'
  ],

  'Sneaking in the Backdoor': [
    'Westside Road',
    'Paul Peak trhd',
    'Carbon River trhd',
    'Lake Eleanor trhd',
    'Huckleberry Creek'
  ],

  'Wonderland Camps': [
    'Pyramid Creek',
    'Devil\'s Dream',
    'South Puyallup River',
    'Klapatche Park',
    'North Puyallup River',
    'Golden Lakes',
    'South Mowich River',
    'Mowich Lake',
    'Ipsut Creek',
    'Eagle\'s Roost',
    'Cataract Valley',
    'Carbon River Camp',
    'Dick Creek',
    'Mystic Camp',
    'Granite Creek',
    'Sunrise Camp',
    'White River',
    'Summerland',
    'Indian Bar',
    'Nickel Creek',
    'Maple Creek',
    'Paradise River'
  ],

  'Northern Loop': [
    'Yellowstone Cliffs',
    'James Camp',
    'Fire Creek',
    'Berkeley Park'
  ],

  'Eastside Camps': [
    'Olallie Creek',
    'Tamanos Creek',
    'Deer Creek',
    'Three Lakes',
    'Dewey Lake'
  ],

  'Other Camps': [
    'Lake George',
    'Lake Eleanor',
    'Forest Lake',
    'Glacier Basin',
    'Snow Lake',
    'Cougar Rock',
    'Longmire'
  ]
};


const trailheadCategories = [
  'Wonderland Trailheads',
  'Alternative Accesses',
  'Eastside Trailheads',
  'Sneaking in the Backdoor'
];


const campsiteCategories = [
  'Wonderland Camps',
  'Northern Loop',
  'Eastside Camps',
  'Other Camps'
];


const viaOptions = [
  '',
  'Spray Park',
  'Sunrise',
  'Windy Gap',
  'Mystic Lake',
  'Glacier Basin',
  'Panhandle',
  'Eastside tr',
  'PCT',
  'St. Andrews tr',
  'N. Puyallup tr',
  'Sunset Park'
];


// There are two algorithms for handling alternate route 'via' selections:
//
// 1. For most 'via' options, relevant trail segments are marked with a Via
//    property. Routes that don't pass through one of these segments are rejected.
//
// 2. For the more complex areas around Spray Park and the Westside Road, nearby
//    trail segments are marked using both Via and IsNearTo properties. Pathfinding
//    rejects any segments that are marked Via:<x> when the via <x> option is not
//    set. When via <x> is selected, pathfinding rejects routes that pass through
//    segments marked IsNearTo without also going Via.
const viaModeNearTo = {
  'Spray Park':     true,
  'N. Puyallup tr': true
};


const isResupply = {
  'Longmire':    true,
  'Mowich Lake': true,
  'White River': true
};


const hasNoGroupSite = {
  'Pyramid Creek':      'Devil\'s Dream',
  'Klapatche Park':     'North Puyallup River',
  'Eagle\'s Roost':     'Mowich Lake',
  'Dick Creek':         'Carbon River Camp',
  'Yellowstone Cliffs': 'James Camp',
  'Deer Creek':         'Tamanos Creek',
  'Forest Lake':        'Sunrise Camp',
  'Snow Lake':          'Maple Creek'
};


// Static map data about trail distances and elevation changes.
const trailData = {
  'Longmire': [
    { To: 'Pyramid Creek', Distance: 3.3, Up: 1400, Down: 400 }
  ],

  'Pyramid Creek': [
    { To: 'Devil\'s Dream', Distance: 2.5, Up: 1400, Down: 100 }
  ],

  'Devil\'s Dream': [
    { To: 'South Puyallup River', Distance: 6.5, Up: 1900, Down: 2700 }
  ],

  'South Puyallup River': [
    { To: 'Klapatche Park', Distance: 4.1, Up: 2100, Down: 800, IsNearTo: 'N. Puyallup tr' }
  ],

  'Klapatche Park': [
    { To: 'North Puyallup River', Distance: 2.6, Up: 100, Down: 1900, IsNearTo: 'N. Puyallup tr' }
  ],

  'North Puyallup River': [
    { To: 'Golden Lakes', Distance: 5.1, Up: 1900, Down: 600, Via: 'Sunset Park' }
  ],

  'Golden Lakes': [
    { To: 'South Mowich River', Distance: 5.8, Up: 200, Down: 2400 }
  ],

  'South Mowich River': [
    { To: 'Mowich Lake', Distance: 4.4, Up: 2400, Down: 300 }
  ],

  'Mowich Lake': [
    { To: 'Ipsut Pass', Distance: 1.4, Up: 400, Down: 300, IsNearTo: 'Spray Park' },
    { To: 'Eagle\'s Roost', Distance: 2, Up: 500, Down: 600 }
  ],

  'Ipsut Pass': [
    { To: 'Ipsut Creek', Distance: 4.2, Up: 0, Down: 2700, IsNearTo: 'Spray Park' },
    { To: 'Carbon River Camp', Distance: 7.4, Up: 1000, Down: 2800, IsNearTo: 'Spray Park' },
  ],

  'Ipsut Creek': [
    { To: 'Carbon River Camp', Distance: 4, Up: 1300, Down: 400, IsNearTo: 'Spray Park' }
  ],

  'Eagle\'s Roost': [
    { To: 'Spray Park', Distance: 3.5, Up: 1600, Down: 600, Via: 'Spray Park' }
  ],

  'Spray Park': [
    { To: 'Cataract Valley', Distance: 1.4, Up: 0, Down: 1400, Via: 'Spray Park' }
  ],

  'Cataract Valley': [
    { To: 'Carbon River Camp', Distance: 1.6, Up: 100, Down: 1300 }
  ],

  'Carbon River Camp': [
    { To: 'Dick Creek', Distance: 1.3, Up: 1000, Down: 100 }
  ],

  'Dick Creek': [
    { To: 'Mystic Camp', Distance: 3.7, Up: 2100, Down: 600, Via: 'Mystic Lake' }
  ],

  'Mystic Camp': [
    { To: 'Granite Creek', Distance: 4.1, Up: 1500, Down: 1200, Via: 'Mystic Lake' }
  ],

  'Granite Creek': [
    { To: 'Frozen Lake', Distance: 3.6, Up: 1400, Down: 500 }
  ],

  'Frozen Lake': [
    { To: 'Sunrise Camp', Distance: 1, Up: 0, Down: 500 }
  ],

  'Sunrise Camp': [
    { To: 'White River', Distance: 3.5, Up: 100, Down: 2100 }
  ],

  'Sunrise': [
    { To: 'White River', Distance: 3.1, Up: 0, Down: 2200, Via: 'Sunrise' },
    { To: 'Sunrise Camp', Distance: 1.4, Up: 200, Down: 400, Via: 'Sunrise' },
    { To: 'Frozen Lake', Distance: 1.6, Up: 400, Down: 100, Via: 'Sunrise' }
  ],

  'White River': [
    { To: 'Fryingpan Creek', Distance: 2.7, Up: 100, Down: 600 }
  ],

  'Fryingpan Creek': [
    { To: 'Summerland', Distance: 4.3, Up: 2200, Down: 100 }
  ],

  'Summerland': [
    { To: 'Indian Bar', Distance: 4.7, Up: 1200, Down: 2100, Via: 'Panhandle' }
  ],

  'Indian Bar': [
    { To: 'Nickel Creek', Distance: 6.8, Up: 1400, Down: 3200 }
  ],

  'Nickel Creek': [
    { To: 'Box Canyon', Distance: 0.9, Up: 100, Down: 400 }
  ],

  'Box Canyon': [
    { To: 'Maple Creek', Distance: 2.7, Up: 500, Down: 700 }
  ],

  'Maple Creek': [
    { To: 'Reflection Lakes', Distance: 4.7, Up: 2300, Down: 200 }
  ],

  'Reflection Lakes': [
    { To: 'Paradise River', Distance: 2.6, Up: 200, Down: 1200 }
  ],

  'Paradise River': [
    { To: 'Longmire', Distance: 3.6, Up: 100, Down: 1200 }
  ],

  'Westside Road': [
    { To: 'Lake George', Distance: 4.8, Up: 1500, Down: 100 },
    { To: 'South Puyallup River', Distance: 6.1, Up: 1700, Down: 400, IsNearTo: 'N. Puyallup tr' }
  ],

  'Lake George': [
    { To: 'South Puyallup River', Distance: 3.1, Up: 800, Down: 900, IsNearTo: 'N. Puyallup tr' }
  ],

  'St. Andrews Creek Trail': [
    { To: 'Westside Road', Distance: 7.7, Up: 700, Down: 1600 },
    { To: 'Lake George', Distance: 4.8, Up: 1100, Down: 600 },
    { To: 'Klapatche Park', Distance: 2.6, Up: 1800, Down: 100, Via: 'St. Andrews tr', IsNearTo: 'N. Puyallup tr' },
  ],

  'North Puyallup Trail': [
    { To: 'Westside Road', Distance: 9, Up: 700, Down: 1900 },
    { To: 'Lake George', Distance: 6, Up: 1100, Down: 900 },
    { To: 'North Puyallup River', Distance: 3, Up: 400, Down: 800, Via: 'N. Puyallup tr' },
  ],

  'Paul Peak trhd': [
    { To: 'Mowich Lake', Distance: 6.5, Up: 2400, Down: 1200 },
    { To: 'South Mowich River', Distance: 4.2, Up: 500, Down: 1400 }
  ],

  'Carbon River trhd': [
    { To: 'Ipsut Creek', Distance: 5.3, Up: 700, Down: 100 }
  ],

  'Forest Lake': [
    { To: 'Sunrise', Distance: 2.6, Up: 1300, Down: 500, Via: 'Sunrise' },
    { To: 'Frozen Lake', Distance: 2.8, Up: 1400, Down: 300 }
  ],

  'Huckleberry Creek': [
    { To: 'Forest Lake', Distance: 6.9, Up: 2900, Down: 200 }
  ],

  'Glacier Basin': [
    { To: 'White River', Distance: 3.6, Up: 100, Down: 1800, Via: 'Glacier Basin' },
    { To: 'Sunrise Camp', Distance: 4.9, Up: 2000, Down: 1700, Via: 'Glacier Basin' },
    { To: 'Frozen Lake', Distance: 4.2, Up: 2000, Down: 1200, Via: 'Glacier Basin' }
  ],

  'Snow Lake': [
    { To: 'Maple Creek', Distance: 5, Up: 500, Down: 2400 },
    { To: 'Reflection Lakes', Distance: 2.8, Up: 700, Down: 500 }
  ],

  'Cougar Rock': [
    { To: 'Longmire', Distance: 1.6, Up: 100, Down: 400 },
    { To: 'Paradise River', Distance: 2.2, Up: 800, Down: 0 }
  ],

  'Yellowstone Cliffs': [
    { To: 'Ipsut Creek', Distance: 5.3, Up: 300, Down: 3100, IsNearTo: 'Spray Park' },
    { To: 'Carbon River Camp', Distance: 4, Up: 600, Down: 2500, IsNearTo: 'Spray Park' },
    { To: 'Ipsut Pass', Distance: 8.6, Up: 2900, Down: 3000, IsNearTo: 'Spray Park' },
    { To: 'James Camp', Distance: 3.3, Up: 900, Down: 1500, Via: 'Windy Gap' }
  ],

  'James Camp': [
    { To: 'Fire Creek', Distance: 4.9, Up: 1700, Down: 1600 },
    { To: 'Berkeley Park', Distance: 8, Up: 3000, Down: 1900 }
  ],

  'Fire Creek': [
    { To: 'Berkeley Park', Distance: 4.1, Up: 1600, Down: 600 }
  ],

  'Berkeley Park': [
    { To: 'Granite Creek', Distance: 4.4, Up: 1300, Down: 1000 },
    { To: 'Frozen Lake', Distance: 2.5, Up: 1200, Down: 0 }
  ],

  'Lake Eleanor trhd': [
    { To: 'Lake Eleanor', Distance: 1.1, Up: 500, Down: 0 }
  ],

  'Lake Eleanor': [
    { To: 'Berkeley Park', Distance: 5.8, Up: 1300, Down: 700 },
    { To: 'Fire Creek', Distance: 5.2, Up: 1000, Down: 1400 },
    { To: 'James Camp', Distance: 9.1, Up: 2300, Down: 2800 }
  ],

  'G. of Patriarchs trhd': [
    { To: 'Olallie Creek', Distance: 3.3, Up: 1800, Down: 0 },
    { To: 'Deer Creek', Distance: 6.9, Up: 1300, Down: 600, Via: 'Eastside tr' },
    { To: 'Three Lakes', Distance: 6.5, Up: 2800, Down: 400 }
  ],

  'Olallie Creek': [
    { To: 'Nickel Creek', Distance: 3.3, Up: 800, Down: 1500 },
    { To: 'Indian Bar', Distance: 6.1, Up: 2500, Down: 1400 }
  ],

  'Upper Owyhigh trhd': [
    { To: 'Fryingpan Creek', Distance: 0.9, Up: 200, Down: 100 },
    { To: 'Tamanos Creek', Distance: 3.1, Up: 1600, Down: 100 }
  ],

  'Tamanos Creek': [
    { To: 'Deer Creek', Distance: 5.5, Up: 200, Down: 2600 }
  ],

  'Deer Creek': [
    { To: 'Tipsoo Lake', Distance: 4.2, Up: 2500, Down: 100 }
  ],

  'Deer Creek trhd': [
    { To: 'Deer Creek', Distance: 0.4, Up: 0, Down: 300 }
  ],

  'Three Lakes': [
    { To: 'Dewey Lake', Distance: 9.5, Up: 2300, Down: 1800, Via: 'PCT' }
  ],

  'Dewey Lake': [
    { To: 'Tipsoo Lake', Distance: 3.3, Up: 900, Down: 700 }
  ],

  'Tipsoo Lake': []
};


function GetTrailSegmentId(startPlace, endPlace)
{
  // Canonicalize into alphabetical order, so we don't need to worry about direction.
  if (startPlace > endPlace) {
    [ startPlace, endPlace ] = [ endPlace, startPlace ];
  }

  return (startPlace + '-' + endPlace).replace(/[ '\.]/g, '');
}


// On load, run some preprocessing over our trail route data.
function PreprocessTrailData()
{
  // Create maplayer elements for every trail segment.
  var map = document.getElementById('map');

  for (var trail in trailData) {
    trailData[trail].forEach(trailLink => {
      var id = GetTrailSegmentId(trail, trailLink.To);

      var img = document.createElement('img');

      img.setAttribute('class', 'maplayer');
      img.setAttribute('id', id);
      img.setAttribute('src', 'Overlays/' + id + '.png');
      img.setAttribute('width', '600');
      img.setAttribute('height', '506');

      map.appendChild(img);
    });
  }

  // Create reversed copies of all the trail links, to make the graph bidirectional.
  var toReverse = {};

  for (var trail in trailData) {
    toReverse[trail] = trailData[trail].slice();
  }

  for (var fromTrail in toReverse) {
    toReverse[fromTrail].forEach(trailLink => {
      trailData[trailLink.To].push({
        To: fromTrail,
        Distance: trailLink.Distance,
        Up: trailLink.Down,
        Down: trailLink.Up,
        Via: trailLink.Via,
        IsNearTo: trailLink.IsNearTo
      });
    });
  }
}

PreprocessTrailData();


// The core place-to-place distance computation.
function FindRoute(startPlace, endPlace, goVia)
{
  // Seed the search with our starting point.
  var searchPaths = [ 
    {
      Place: startPlace,
      Route: [],
      Distance: 0,
      Up: 0,
      Down: 0
    }
  ];

  var found;

  // Breadth-first iterative graph traversal.
  while (searchPaths.length > 0) {
    var newPaths = [];

    searchPaths.forEach(searchPath => {
      if (searchPath.Place == endPlace) {
        // If a specific 'via' option is enabled, reject routes that do not satisfy that constraint.
        if (goVia &&
            !searchPath.WentVia &&
            (searchPath.WentNearTo || !viaModeNearTo[goVia])) {
          return;
        }

        // Reached the goal! But is this the shortest path we've found so far?
        if (!found || (searchPath.Distance < found.Distance)) {
          searchPath.Route.push(endPlace);
          found = searchPath;
        }
      }
      else {
        trailData[searchPath.Place].forEach(trailLink => {
          // Prevent cycles.
          if (searchPath.Route.includes(trailLink.To)) {
            return;
          }

          // Early-out if we already found a shorter route than the one currently being explored.
          var newDistance = searchPath.Distance + trailLink.Distance;

          if (found && (newDistance >= found.Distance)) {
            return;
          }

          // Some links are conditionally valid depending on which 'via' option was selected.
          if (viaModeNearTo[trailLink.Via] && (trailLink.Via != goVia)) {
            return;
          }

          newPaths.push({
            Place: trailLink.To,
            Route: [...searchPath.Route, searchPath.Place],
            Distance: newDistance,
            Up: searchPath.Up + trailLink.Up,
            Down: searchPath.Down + trailLink.Down,
            WentVia: searchPath.WentVia || (trailLink.Via == goVia),
            WentNearTo: searchPath.WentNearTo || (trailLink.IsNearTo == goVia)
          });
        });
      }
    });

    searchPaths = newPaths;
  }

  return found;
}


function MarkResupplyLocations(camp, index)
{
  if (isResupply[camp] && index > 0 && camp != trip.EndTrailhead) {
    return camp + ' [resupply]';
  }
  else {
    return camp;
  }
}


function MeasureDistance(day)
{
  // What two places are we measuring between?
  var startPlace = (day == 0) ? trip.StartTrailhead : trip.SelectedCampsites[day - 1];

  var endPlace = (day == trip.Duration - 1) ? trip.EndTrailhead : trip.SelectedCampsites[day];

  // Find the best route.
  var route = FindRoute(startPlace, endPlace, trip.Via[day]);

  // Store and format the results.
  dayRoutes[day] = route;

  if (!route) {
    return 'error: no route found!';
  }

  var miles = route.Distance.toFixed(1) + ' miles';

  var up = '&uarr; ' + route.Up + '\'';
  var down = '&darr; ' + route.Down + '\'';

  var align = '<span style="visibility:hidden">0</span>';

  var tooltip = '<span class="tip">' + ExpandPlaceNames(route.Route.map(MarkResupplyLocations).join(' &rarr; ')) + '</span>';

  if (miles.length < 10) {
    miles = align + miles;
  }

  if (up.length < 12) {
    up = up + align.repeat(12 - up.length);
  }

  return miles + ', ' + up + ' ' + down + tooltip;
}


function ExpandPlaceNames(name)
{
  return name.replace(/trhd/g, 'trailhead')
             .replace(/G. of/g, 'Grove of the');
}


function EncodePlaceNames(name)
{
  return name.replace(/trailhead/g, 'trhd')
             .replace(/Grove of the/g, 'G. of');
}


function SprayParkToString(viaSprayPark)
{
  return viaSprayPark ? 'Spray Park' : '';
}


function GenerateItinerary(ccw)
{
  // First, we simplify the problem down to one involving just four major
  // trailheads, replacing other trailheads with the closest of the chosen four.

  const simplifiedTrailheads = {
    'Longmire':              'Longmire',
    'Mowich Lake':           'Mowich Lake',
    'Sunrise':               'White River',
    'White River':           'White River',
    'Box Canyon':            'Box Canyon',
    'Fryingpan Creek':       'White River',
    'Reflection Lakes':      'Box Canyon',
    'G. of Patriarchs trhd': 'Box Canyon',
    'Upper Owyhigh trhd':    'White River',
    'Deer Creek trhd':       'White River',
    'Tipsoo Lake':           'White River',
    'Westside Road':         'Longmire',
    'Paul Peak trhd':        'Mowich Lake',
    'Carbon River trhd':     'Mowich Lake',
    'Lake Eleanor trhd':     'White River',
    'Huckleberry Creek':     'White River'
  };

  const otherTrailheads = {
    'Longmire':    [ 'Mowich Lake', 'White River', 'Box Canyon'  ],
    'Mowich Lake': [ 'White River', 'Box Canyon',  'Longmire'    ],
    'White River': [ 'Box Canyon',  'Longmire',    'Mowich Lake' ],
    'Box Canyon':  [ 'Longmire',    'Mowich Lake', 'White River' ]
  };

  var simpleStart = simplifiedTrailheads[trip.StartTrailhead];
  var simpleEnd = simplifiedTrailheads[trip.EndTrailhead];

  var trailheads = otherTrailheads[simpleStart];

  // Figure out which order to visit these trailheads, handling clockwise vs.
  // counterclockwise preference and the possibility of this being a section hike.
  if (ccw) {
    trailheads = trailheads.slice();
    trailheads.reverse();
  }

  if (simpleStart == simpleEnd) {
    // Doing a full circuit of all four sections.
    trailheads = [ simpleStart, ...trailheads, simpleEnd ];
  }
  else if (simpleEnd == trailheads[0]) {
    // Single section.
    trailheads = [ simpleStart, simpleEnd ];
  }
  else if (simpleEnd == trailheads[1]) {
    // Two sections.
    trailheads = [ simpleStart, trailheads[0], simpleEnd ];
  }
  else {
    // Three sections.
    trailheads = [ simpleStart, trailheads[0], trailheads[1], simpleEnd ];
  }

  // Hack to make sure Ipsut Creek is considered when not going over Spray Park,
  // even though it's slightly off the WT so normal route finding ignores it.
  if (!trip.ViaSprayPark) {
    for (var i = 0; i < trailheads.length; i++) {
      if (trailheads[i] == 'Mowich Lake') {
        if (ccw && i > 0) {
          trailheads.splice(i, 0, 'Ipsut Creek');
          i++;
        }
        else if (!ccw && i < trailheads.length - 1) {
          trailheads.splice(i + 1, 0, 'Ipsut Creek');
          i++;
        }
      }
    }
  }

  // Restore the original non-simplified start and finish trailheads.
  trailheads[0] = trip.StartTrailhead;
  trailheads[trailheads.length - 1] = trip.EndTrailhead;

  // Find routes between the chosen trailheads, and flatten these into a single path.
  var route = [ trailheads[0] ];

  for (var i = 0; i < trailheads.length - 1; i++) {
    var via = SprayParkToString(trip.ViaSprayPark);

    var section = FindRoute(trailheads[i], trailheads[i + 1], via);

    route = route.concat(section.Route.slice(1));
  }

  // Measure cumulative distance along our chosen route.
  var totalDistance = 0;
  var distances = [];
  var isSprayPark = [];
  var nearSprayPark = [];

  for (var i = 0; i < route.length - 1; i++) {
    var trailLink = trailData[route[i]].find(link => link.To == route[i + 1]);

    totalDistance += trailLink.Distance;
    distances.push(totalDistance);
    isSprayPark.push(trailLink.Via == 'Spray Park');
    nearSprayPark.push(trailLink.IsNearTo == 'Spray Park');
  }

  // Remove the start and end trailheads.
  route.shift();

  route.pop();
  distances.pop();

  // Filter out entries for places that aren't actually campsites.
  var allCampsites = [].concat(...campsiteCategories.map(category => placeNames[category]))
                       .filter(camp => !(trip.UseGroupSites && hasNoGroupSite[camp]));

  var i = 0;

  while (i < route.length) {
    if (allCampsites.includes(route[i]) && route[i] != 'Longmire') {
      i++;
    }
    else {
      route.splice(i, 1);
      distances.splice(i, 1);
      isSprayPark.splice(i, 1);
      nearSprayPark.splice(i, 1);
    }
  }

  // Choose camps as close as possible to an even division of the total distance.
  var camps = [];
  var currentCamp = 0;

  for (var day = 0; day < trip.Duration - 1; day++) {
    var wantedDistance = totalDistance * (day + 1) / trip.Duration;

    while (distances[currentCamp] < wantedDistance && currentCamp < route.length - 1) {
      currentCamp++;
    }

    if (currentCamp > 0) {
      var prevDistance = wantedDistance - distances[currentCamp - 1];
      var nextDistance = distances[currentCamp] - wantedDistance;

      camps.push((nextDistance < prevDistance) ? currentCamp : currentCamp - 1);
    }
    else {
      camps.push(currentCamp);
    }
  }

  // Add dummy entries at both ends of the route, so bound checks are not needed later on.
  camps.push(route.length);
  distances.push(totalDistance);

  camps[-1] = -1;
  distances[-1] = 0;

  // Unsophisticated easing function attempts to smooth out the mileage per day.
  function RateCampChoice(day, proposedCamp)
  {
    var prevCamp = camps[day - 1];
    var nextCamp = camps[day + 1];

    var prevDistance = distances[proposedCamp] - distances[prevCamp];
    var nextDistance = distances[nextCamp] - distances[proposedCamp];

    return Math.abs(prevDistance - nextDistance);
  }

  for (var day = 0; day < trip.Duration - 1; day++) {
    var delta = RateCampChoice(day, camps[day]);

    // Maybe we should slide our camp choice one place earlier?
    var isAheadOfPrevCamp = camps[day] > Math.max(camps[day - 1], 0);

    var deltaPrev = isAheadOfPrevCamp ? RateCampChoice(day, camps[day] - 1) : Number.MAX_VALUE;

    // Or perhaps we should slide it later?
    var isBehindNextCamp = camps[day] < Math.min(camps[day + 1], route.length - 1);

    var deltaNext = isBehindNextCamp ? RateCampChoice(day, camps[day] + 1) : Number.MAX_VALUE;

    if (deltaPrev < delta && deltaPrev < deltaNext) {
      camps[day]--;
    }
    else if (deltaNext < delta) {
      camps[day]++;
    }
  }

  // Store our chosen route, and refresh the UI.
  for (var day = 0; day < trip.Duration - 1; day++) {
    trip.SelectedCampsites[day] = route[camps[day]];
  }

  for (var day = 0; day < trip.Duration; day++) {
    var sliceStart = camps[day - 1] + 1;
    var sliceEnd = camps[day] + 1;

    var isSpray = isSprayPark.slice(sliceStart, sliceEnd).some(value => value);
    var nearSpray = nearSprayPark.slice(sliceStart, sliceEnd).some(value => value);

    trip.Via[day] = SprayParkToString(trip.ViaSprayPark && (isSpray || !nearSpray));
  }

  RecreateUIElements();
  SaveTrip();
}


// UI code.
function InitializeUI()
{
  document.getElementById('tripduration').value = trip.Duration;

  document.getElementById('usegroupsites').checked = trip.UseGroupSites;

  RecreateUIElements();
}


function CreatePlaceSelector(categories, currentSelection, changeHandler, useGroupSites)
{
  var result = '<select onChange="' + changeHandler + '">';

  categories.forEach(category => {
    result += '<optgroup label="' + category + '">';

    placeNames[category].forEach(placeName => {
      if (!(useGroupSites && hasNoGroupSite[placeName])) {
        var selectedAttribute = (placeName == currentSelection) ? ' selected' : '';
        result += '<option' + selectedAttribute + '>' + placeName + '</option>';
      }
    });

    result += '</optgroup>';
  });

  return result + '</select>';
}


function GetViaSelectorClass(value)
{
  var valueIsMeaningful;

  if (trip.ViaSprayPark) {
    valueIsMeaningful = value != 'Spray Park';
  }
  else {
    valueIsMeaningful = Boolean(value);
  }

  return valueIsMeaningful ? '' : 'plannerinactiveselector';
}


function CreateViaSelector(currentSelection, day)
{
  var selectorClass = GetViaSelectorClass(currentSelection);

  var result = '<select class="' + selectorClass + '" onChange="ViaChanged(' + day + ', this.value)">';

  viaOptions.forEach(via => {
    var selectedAttribute = (via == currentSelection) ? ' selected' : '';
    result += '<option' + selectedAttribute + '>' + via + '</option>';
  });

  return result + '</select>';
}


function CreateResupplyIcon(day)
{
  var resupplies = dayRoutes[day].Route.slice(1).filter(camp => isResupply[camp]);

  if (!resupplies.length || day == trip.Duration - 1) {
    return '';
  }
  else {
    return '<img src="bucket.png"><span class="tip">Resupply at ' + resupplies.join(', ') + '</span></img>';
  }
}


function CreateTableRow(day)
{
  if (day == 0) {
    // Starting the first day at trailhead.
    var startHtml = CreatePlaceSelector(trailheadCategories, trip.StartTrailhead, 'StartTrailheadChanged(this.value)', false);
  }
  else {
    // Starting a subsequent day at previous night's camp.
    var startHtml = trip.SelectedCampsites[day - 1];
  }

  if (day == trip.Duration - 1) {
    // Ending the trip back at a trailhead.
    var endHtml = CreatePlaceSelector(trailheadCategories, trip.EndTrailhead, 'EndTrailheadChanged(this.value)', false);
  }
  else {
    // Ending the day at a campsite.
    var endHtml = CreatePlaceSelector(campsiteCategories, trip.SelectedCampsites[day], 'CampsiteChanged(' + day + ', this.value)', trip.UseGroupSites);
  }

  var viaHtml = CreateViaSelector(trip.Via[day], day);

  return '<tr>' +
         '  <td class="plannerday">Day ' + (day + 1) + '</td>' +
         '  <td>' + startHtml + '</td>' +
         '  <td>to ' + endHtml + '</td>' +
         '  <td>via ' + viaHtml + '</td>' +
         '  <td class="plannerdistance tooltip" onMouseEnter="OnEnterDay(' + day + ')" onMouseLeave="OnLeaveDay(' + day + ')">' + MeasureDistance(day) + '</td>' +
         '  <td class="plannerresupply tooltip">' + CreateResupplyIcon(day) + '</td>' +
         '</tr>';
}


function RecreateUIElements()
{
  var table = document.getElementById('table');

  var checkboxAttribute = trip.ViaSprayPark ? 'checked ' : '';

  table.innerHTML = '<tr class="plannerspraymaster">' +
                    '  <td/>' +
                    '  <td/>' +
                    '  <td/>' +
                    '  <td>via Spray Park: <input type="checkbox" onChange="ViaSprayParkChanged(this.checked)" ' + checkboxAttribute + '/></td>' +
                    '</tr>';

  for (var day = 0; day < trip.Duration; day++) {
    var row = document.createElement('tr');
    row.innerHTML = CreateTableRow(day);
    table.appendChild(row);
  }

  UpdateTotals();
  UpdateMapOverlays();
  ShowOrHideSelectedCampDetails();
}


function UpdateTotals()
{
  var distance = 0;
  var up = 0;
  var down = 0;

  for (var i = 0; i < trip.Duration; i++) {
    if (dayRoutes[i]) {
      distance += dayRoutes[i].Distance;
      up += dayRoutes[i].Up;
      down += dayRoutes[i].Down;
    }
  }

  document.getElementById('totals').innerHTML = distance.toFixed(1) + ' miles, &uarr; ' + up + '\' &darr; ' + down + '\'';
}


function ShowOrHideSelectedCampDetails()
{
  var cougarDisplay = 'none';
  var deweyDisplay = 'none';
  var longmireDisplay = 'none';

  for (var i = 0; i < trip.Duration - 1; i++) {
    if (trip.SelectedCampsites[i] == 'Cougar Rock') {
      cougarDisplay = 'inline';
    }
    else if (trip.SelectedCampsites[i] == 'Dewey Lake') {
      deweyDisplay = 'inline';
    }
    else if (trip.SelectedCampsites[i] == 'Longmire') {
      longmireDisplay = 'inline';
    }
  }

  document.getElementById('cougarrock').style.display = cougarDisplay;
  document.getElementById('deweylake').style.display = deweyDisplay;
  document.getElementById('longmire').style.display = longmireDisplay;
}


function TripDurationChanged(newDuration)
{
  trip.Duration = newDuration;

  RecreateUIElements();
  SaveTrip();
}


function UseGroupSitesChanged(newValue)
{
  trip.UseGroupSites = newValue;

  if (trip.UseGroupSites) {
    // Fix up any selected camps that are no longer valid.
    for (var i = 0; i < trip.SelectedCampsites.length; i++) {
      var replacement = hasNoGroupSite[trip.SelectedCampsites[i]];

      if (replacement) {
        trip.SelectedCampsites[i] = replacement;
      }
    }
  }

  RecreateUIElements();
  SaveTrip();
}


function GetTableRow(day)
{
  return document.getElementById('table').children[day + 1];
}


function UpdateDistance(day)
{
  var row = GetTableRow(day);

  row.children[4].innerHTML = MeasureDistance(day);
  row.children[5].innerHTML = CreateResupplyIcon(day)
}


function StartTrailheadChanged(newValue)
{
  trip.StartTrailhead = newValue;

  UpdateDistance(0);
  UpdateTotals();
  UpdateMapOverlays();
  SaveTrip();
}


function EndTrailheadChanged(newValue)
{
  trip.EndTrailhead = newValue;

  UpdateDistance(trip.Duration - 1);
  UpdateTotals();
  UpdateMapOverlays();
  SaveTrip();
}


function CampsiteChanged(day, newValue)
{
  trip.SelectedCampsites[day] = newValue;

  GetTableRow(day + 1).children[1].innerHTML = newValue;

  UpdateDistance(day);
  UpdateDistance(day + 1);
  UpdateTotals();
  UpdateMapOverlays();
  ShowOrHideSelectedCampDetails();
  SaveTrip();
}


function ViaChanged(day, newValue)
{
  trip.Via[day] = newValue;

  UpdateDistance(day);
  UpdateTotals();
  UpdateMapOverlays();
  SaveTrip();

  GetTableRow(day).children[3].children[0].className = GetViaSelectorClass(newValue);
}


function ViaSprayParkChanged(newValue)
{
  trip.ViaSprayPark = newValue;

  for (var i = 0; i < maxDays; i++) {
    trip.Via[i] = SprayParkToString(newValue);
  }

  RecreateUIElements();
  SaveTrip();
}


function UpdateMapOverlays()
{
  // Make the desired map segments visible.
  var visible = {};

  for (var i = 0; i < trip.Duration; i++) {
    var isHighlighted = highlightedDays & (1 << i);

    var colorForDay = 1 + (i % 3);

    // Make sure loops have different adjacent colors.
    if (i == trip.Duration - 1 && colorForDay == 1) {
      colorForDay = 2;
    }

    var className = isHighlighted ? 'plannermaphighlighted' :
                                    'plannermapday' + colorForDay;

    for (var j = 0; j < dayRoutes[i].Route.length - 1; j++) {
      var id = GetTrailSegmentId(dayRoutes[i].Route[j], dayRoutes[i].Route[j + 1]);

      if (!visible[id] || isHighlighted) {
        var element = document.getElementById(id);

        if (element) {
          element.className = 'maplayer ' + className;

          visible[id] = true;
        }
      }
    }
  }

  // Hide any previously visible map segments that are no longer wanted.
  for (var id in visibleMapSegments) {
    if (!visible[id]) {
      document.getElementById(id).className = 'maplayer';
    }
  }

  visibleMapSegments = visible;
}


function OnEnterDay(day)
{
  highlightedDays |= 1 << day;

  UpdateMapOverlays();
}


function OnLeaveDay(day)
{
  highlightedDays &= ~(1 << day);

  UpdateMapOverlays();
}


// Import and export functions.
function ExportTrip()
{
  var data = SerializeTrip();

  var exporter = document.createElement('a');
  exporter.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(data));
  exporter.setAttribute('download', 'itinerary.txt');
  exporter.style.display = 'none';

  document.body.appendChild(exporter);

  exporter.click();

  document.body.removeChild(exporter);
}


function ImportTrip()
{
  document.getElementById("fileselector").click();
}


function ImportChanged(input)
{
  var file = input.files[0];

  if (file) {
    var reader = new FileReader();

    reader.onload = function() {
      DeserializeTrip(reader.result);
      InitializeUI();
      SaveTrip();
    };

    reader.readAsText(file);
  }

  input.value = null;
}


function SerializeTrip()
{
  var text = trip.Duration + ' days\n';

  if (trip.UseGroupSites) {
    text += 'using group campsites\n';
  }

  if (trip.ViaSprayPark) {
    text += 'via Spray Park\n';
  }

  for (var i = 0; i < trip.Duration; i++) {
    var startPlace = (i == 0) ? trip.StartTrailhead : trip.SelectedCampsites[i - 1];
    var endPlace = (i == trip.Duration - 1) ? trip.EndTrailhead : trip.SelectedCampsites[i];

    text += '\nDay ' + (i + 1) + ': ' + ExpandPlaceNames(startPlace) + ' to ' + ExpandPlaceNames(endPlace);

    if (trip.Via[i] && (!trip.ViaSprayPark || trip.Via[i] != 'Spray Park')) {
      text += ' (via ' + trip.Via[i] + ')';
    }
    else if (!trip.Via[i] && trip.ViaSprayPark) {
      text += ' (not Spray Park)';
    }

    text += '\n';

    text += dayRoutes[i].Distance.toFixed(1) + ' miles, ' + dayRoutes[i].Up + '\' gain, ' + dayRoutes[i].Down + '\' descent\n';

    text += '  (route: ' + ExpandPlaceNames(dayRoutes[i].Route.map(MarkResupplyLocations).join(' -> ')) + ')\n';
  }

  return text;
}


function DeserializeTrip(text)
{
  CreateDefaultTrip();

  text.split("\n").forEach(line => {
    var match;

    if (match = /^(\d+) days/.exec(line)) {
      trip.Duration = match[1];
    }
    else if (/^using group campsites/.test(line)) {
      trip.UseGroupSites = true;
    }
    else if (/^via Spray Park/.test(line)) {
      trip.ViaSprayPark = true;

      for (var i = 0; i < maxDays; i++) {
        trip.Via[i] = 'Spray Park';
      }
    }
    else if (match = /^Day (\d+): (.+) to (([^ ]| [^\(])+)/.exec(line)) {
      var day = match[1] - 1;

      var startPlace = EncodePlaceNames(match[2]);
      var endPlace = EncodePlaceNames(match[3]);

      if (day == 0) {
        trip.StartTrailhead = startPlace;
      }

      if (day == trip.Duration - 1) {
        trip.EndTrailhead = endPlace;
      }
      else {
        trip.SelectedCampsites[day] = endPlace;
      }

      if (match = /\(via (.+)\)/.exec(line)) {
        trip.Via[day] = match[1];
      }
      else if (line.includes('(not Spray Park)')) {
        trip.Via[day] = '';
      }
    }
  });

  PopulateUnusedCampsites();
}


function ResetTrip()
{
  CreateDefaultTrip();
  PopulateUnusedCampsites();
  InitializeUI();
  SaveTrip();

  if (window.location.href.includes('?')) {
    window.location = window.location.href.split('?')[0];
  }
}


function SaveTrip()
{
  localStorage.Trip = JSON.stringify(trip);
}


function ShareTrip()
{
  // Simplify our trip data, stripping out any unnecessary elements.
  var data = {...trip};

  data.SelectedCampsites = data.SelectedCampsites.slice(0, data.Duration - 1);

  if (data.Via.slice(0, data.Duration).every(value => data.ViaSprayPark ? value == 'Spray Park' : !value)) {
    delete data.Via;
  }
  else {
    data.Via = data.Via.slice(0, data.Duration);
  }

  if (!data.ViaSprayPark) {
    delete data.ViaSprayPark;
  }

  if (!data.UseGroupSites) {
    delete data.UseGroupSites;
  }

  // Encode as a URL.
  var url = window.location.href.split('?')[0];

  var itinerary = JSON.stringify(data);

  var toShare = url + '?i=' + encodeURIComponent(itinerary);

  if (navigator.share) {
    navigator.share({
      title: 'Hiking Tahoma: Planning Tool Itinerary',
      url: toShare
    });
  }
  else {
    const element = document.createElement('textarea');

    element.value = toShare;
    element.setAttribute('readonly', '');
    element.style.position = 'absolute';
    element.style.left = '-9999px';

    document.body.appendChild(element);

    element.select();
    document.execCommand('copy');

    document.body.removeChild(element);

    alert("Itinerary share link copied to clipboard");
  }
}


function RestoreSavedState()
{
  var initData;

  // URL parameter takes priority, if any.
  var search = window.location.search;

  if (search) {
    var searchParams = new URLSearchParams(search);

    var iParam = searchParams.get('i');

    if (iParam) {
      initData = decodeURIComponent(iParam);
    }
  }

  // Otherwise use local storage data.
  if (!initData) {
    initData = localStorage.Trip;

    if (!initData)
      return false;
  }

  // Parse the saved state.
  try {
    trip = JSON.parse(initData);
  }
  catch(e) {
    return false;
  }

  // Fix up older save formats.
  var isOldFormat = trip.hasOwnProperty('ViaSprayParkMaster') || Array.isArray(trip.ViaSprayPark);

  if (isOldFormat) {
    if (trip.ViaSprayPark) {
      trip.Via = trip.ViaSprayPark.map(SprayParkToString);
    }

    trip.ViaSprayPark = trip.ViaSprayParkMaster;

    delete trip.ViaSprayParkMaster;
  }

  // Restore any potentially missing data.
  if (!trip.hasOwnProperty('ViaSprayPark')) {
    trip.ViaSprayPark = false;
  }

  if (!trip.hasOwnProperty('Via')) {
    trip.Via = Array(Number(trip.Duration)).fill(SprayParkToString(trip.ViaSprayPark));
  }

  PopulateUnusedCampsites();

  return true;
}


// Initialize or restore state when the page first loads.
if (!RestoreSavedState()) {
  CreateDefaultTrip();
  PopulateUnusedCampsites();
}

InitializeUI();
